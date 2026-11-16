// Satış ekranı (POS) — Alpine.js bileşeni.
//
// Durum yönetimi (state) kuralları:
//  1) Sunucu "gerçek"tir. Her işlemden sonra sunucu güncel adisyonu ve masa planını döner.
//  2) Ekran beklemez (iyimser güncelleme): ürüne dokununca satır hemen eklenir, istek arkadan gider.
//  3) İstekler sırayla gider (kuyruk). Hızlı hızlı dokunulsa bile sıra karışmaz.
//  4) Bekleyen istek varken sunucu cevabı ekrana yazılmaz; son istek bitince yazılır.
//     Böylece "3 kere dokundum, ekranda bir an 2 göründü" gibi zıplamalar olmaz.

document.addEventListener('alpine:init', () => {
    const veri = JSON.parse(document.getElementById('satis-verisi').textContent);
    const anahtarDegeri = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const paraBicimi = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const saatBicimi = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

    Alpine.data('satisEkrani', () => ({
        // ---------- Durum (state) ----------
        urunler: veri.urunler,
        odemeTurleri: veri.odemeTurleri,
        ekran: veri.ekran,                 // { masalar: [...], paketler: [...] }
        paketKaynaklari: ['Paket', 'Yemeksepeti', 'Getir', 'Trendyol', 'Migros'],

        gorunum: 'plan',                   // 'plan' | 'satis'
        secili: { kaynak: null, masaId: null, adisyonId: null, baslik: '' },
        adisyon: null,                     // sunucudan gelen son adisyon (yoksa null)
        yerelSatirlar: [],                 // ekranda görünen satırlar (iyimser)

        seciliKategori: null,
        arama: '',
        odemeAcik: false,
        iptalAcik: false,
        iptalNedeni: '',
        toast: { yazi: '', tur: '' },

        kuyruk: Promise.resolve(),
        bekleyenIstek: 0,
        simdi: Date.now(),

        init() {
            this.seciliKategori = this.kategoriler[0] ?? null;
            // Masa planı arada bir yenilensin (başka bilgisayar/telefon da kullanırsa) ve süreler ilerlesin
            setInterval(() => this.simdi = Date.now(), 30000);
            setInterval(() => { if (this.gorunum === 'plan' && !this.bekleyenIstek) this.planiYenile(); }, 15000);
        },

        // ---------- Türetilen değerler ----------
        get kategoriler() {
            return [...new Set(this.urunler.map(urun => urun.kategori))];
        },

        get gorunenUrunler() {
            const aranan = this.arama.trim().toLocaleLowerCase('tr-TR');
            if (aranan) {
                return this.urunler.filter(urun => urun.ad.toLocaleLowerCase('tr-TR').includes(aranan));
            }
            return this.urunler.filter(urun => urun.kategori === this.seciliKategori);
        },

        get satirlar() { return this.yerelSatirlar; },

        get toplam() {
            return this.yerelSatirlar.reduce((toplam, satir) => toplam + satir.adet * satir.birimFiyat, 0);
        },

        adisyondakiAdet(urunId) {
            return this.yerelSatirlar.filter(satir => satir.urunId === urunId).reduce((adet, satir) => adet + satir.adet, 0);
        },

        // ---------- Biçimlendirme ----------
        para(tutar) { return paraBicimi.format(tutar) + ' ₺'; },
        saat(tarih) { return saatBicimi.format(new Date(tarih)); },
        gecenSure(tarih) {
            if (!tarih) return '';
            const dakika = Math.max(0, Math.floor((this.simdi - new Date(tarih)) / 60000));
            return dakika < 60 ? `${dakika} dk` : `${Math.floor(dakika / 60)} sa ${dakika % 60} dk`;
        },

        // ---------- Ekranlar arası geçiş ----------
        masaAc(masa) {
            this.adisyonaGec({ kaynak: 'Masa', masaId: masa.id, adisyonId: masa.adisyonId, baslik: masa.ad });
        },

        paketAc(kaynak) {
            this.adisyonaGec({ kaynak, masaId: null, adisyonId: null, baslik: `${kaynak} (yeni)` });
        },

        adisyonAc(adisyonId, baslik, kaynak) {
            this.adisyonaGec({ kaynak, masaId: null, adisyonId, baslik });
        },

        async adisyonaGec(secim) {
            this.secili = secim;
            this.adisyon = null;
            this.yerelSatirlar = [];
            this.odemeAcik = this.iptalAcik = false;
            this.arama = '';
            this.gorunum = 'satis';

            if (secim.adisyonId) {
                const cevap = await fetch(`/Satis/Adisyon/${secim.adisyonId}`);
                if (cevap.ok) {
                    this.adisyonuYaz(await cevap.json());
                } else {
                    this.bildir('Bu adisyon kapanmış.', 'hata');
                    this.secili.adisyonId = null;
                    this.planiYenile();
                }
            }
        },

        geriDon() {
            if (this.odemeAcik || this.iptalAcik) {
                this.odemeAcik = this.iptalAcik = false;
                return;
            }
            this.gorunum = 'plan';
        },

        // ---------- Ürün ekle / adet / sil ----------
        urunEkle(urun) {
            // 1) Ekranı hemen güncelle
            const satir = this.yerelSatirlar.find(oge => oge.urunId === urun.id && oge.birimFiyat === urun.fiyat);
            if (satir) {
                satir.adet++;
            } else {
                this.yerelSatirlar.push({ anahtar: 'yeni-' + urun.id, id: null, urunId: urun.id, ad: urun.ad, adet: 1, birimFiyat: urun.fiyat });
            }
            // 2) Sunucuya sırayla gönder (ilk üründe adisyon sunucuda açılır)
            const secim = this.secili;
            this.istekGonder('/Satis/UrunEkle', () => ({
                adisyonId: secim.adisyonId, kaynak: secim.kaynak, masaId: secim.masaId, urunId: urun.id, adet: 1
            }));
        },

        arttir(satir) {
            const urun = this.urunler.find(oge => oge.id === satir.urunId);
            if (urun) {
                this.urunEkle(urun);
            }
        },

        azalt(satir) {
            this.adetYaz(satir, satir.adet - 1);
        },

        sil(satir) {
            this.adetYaz(satir, 0);   // tek dokunuşla satır gider
        },

        adetYaz(satir, yeniAdet) {
            if (!satir.id) return;
            if (yeniAdet <= 0) {
                this.yerelSatirlar = this.yerelSatirlar.filter(oge => oge !== satir);
            } else {
                satir.adet = yeniAdet;
            }
            const secim = this.secili;
            this.istekGonder('/Satis/Adet', () => ({ adisyonId: secim.adisyonId, satirId: satir.id, adet: yeniAdet }));
        },

        // ---------- Hesap / ödeme / iptal ----------
        hesapIste() {
            const istendi = this.adisyon.durum !== 'hesap';
            this.adisyon.durum = istendi ? 'hesap' : 'acik';
            const secim = this.secili;
            this.istekGonder('/Satis/Hesap', () => ({ adisyonId: secim.adisyonId, istendi }));
            if (istendi) {
                this.bildir(`${secim.baslik}: hesap istendi`);
                this.gorunum = 'plan';   // garson masaya döner; masa sarı görünür
            }
        },

        ode(odeme) {
            const secim = this.secili;
            const baslik = secim.baslik;   // cevap gelince başlık değişebilir, şimdiden sakla
            const tutar = this.toplam;
            this.istekGonder('/Satis/Ode', () => ({ adisyonId: secim.adisyonId, odemeTuruId: odeme.id }), () => {
                this.bildir(`${baslik} kapandı · ${this.para(tutar)} · ${odeme.ad}`, 'basari');
            });
            this.odemeAcik = false;
            this.gorunum = 'plan';
        },

        iptalAc() {
            this.iptalNedeni = '';
            this.iptalAcik = true;
            this.$nextTick(() => this.$refs.iptalNedeni.focus());
        },

        iptalEt() {
            const secim = this.secili;
            const baslik = secim.baslik;
            const neden = this.iptalNedeni.trim();
            if (!neden) return;
            this.istekGonder('/Satis/Iptal', () => ({ adisyonId: secim.adisyonId, neden }), () => {
                this.bildir(`${baslik} iptal edildi`);
            });
            this.iptalAcik = false;
            this.gorunum = 'plan';
        },

        // ---------- Sunucu ile konuşma ----------
        // govdeHazirla bir fonksiyon: istek sırası gelince çalışır, böylece önceki
        // istekte açılan adisyonun numarasını (adisyonId) kullanabilir.
        istekGonder(adres, govdeHazirla, basarili) {
            this.bekleyenIstek++;
            const secim = this.secili;

            this.kuyruk = this.kuyruk.then(async () => {
                try {
                    const cevap = await fetch(adres, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': anahtarDegeri },
                        body: JSON.stringify(govdeHazirla())
                    });
                    const sonuc = await cevap.json().catch(() => ({}));
                    if (sonuc.ekran) this.ekran = sonuc.ekran;

                    if (!cevap.ok) {
                        this.bildir(sonuc.hata || 'İşlem yapılamadı.', 'hata');
                        if (this.secili === secim) await this.adisyonuYenile();   // ekranı sunucuya eşitle
                        return;
                    }

                    // Adisyon açıldıysa numarasını hemen sakla: sıradaki istekler bunu kullanır
                    if (sonuc.adisyon) secim.adisyonId = sonuc.adisyon.id;
                    else if ('adisyon' in sonuc) secim.adisyonId = null;   // son satır silindi: masa boşaldı

                    if (this.secili === secim && this.bekleyenIstek === 1) {
                        this.adisyonuYaz(sonuc.adisyon);
                    }
                    basarili?.();
                } catch {
                    this.bildir('Bağlantı hatası. Tekrar dene.', 'hata');
                } finally {
                    this.bekleyenIstek--;
                }
            });
        },

        adisyonuYaz(adisyon) {
            this.adisyon = adisyon;
            if (!adisyon) {
                this.yerelSatirlar = [];
                if (this.secili.kaynak !== 'Masa') this.secili.baslik = `${this.secili.kaynak} (yeni)`;
                return;
            }
            this.secili.adisyonId = adisyon.id;
            this.secili.baslik = adisyon.baslik;
            this.yerelSatirlar = adisyon.satirlar.map(satir => ({ ...satir, anahtar: 'satir-' + satir.id }));
        },

        async adisyonuYenile() {
            if (!this.secili.adisyonId) { this.adisyonuYaz(null); return; }
            const cevap = await fetch(`/Satis/Adisyon/${this.secili.adisyonId}`);
            this.adisyonuYaz(cevap.ok ? await cevap.json() : null);
        },

        async planiYenile() {
            const cevap = await fetch('/Satis/Durum');
            if (cevap.ok) this.ekran = await cevap.json();
        },

        bildir(yazi, tur = '') {
            this.toast = { yazi, tur };
            clearTimeout(this.toastZamani);
            this.toastZamani = setTimeout(() => this.toast = { yazi: '', tur: '' }, 3000);
        }
    }));
});

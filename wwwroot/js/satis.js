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

    // ---------- Renkler (Tailwind sınıfları) ----------
    const masaRenkleri = {
        bos: 'bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800',
        dolu: 'bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/40 dark:text-amber-300 dark:border-amber-800'
    };
    const platformDugmeRenkleri = {
        Paket: 'bg-slate-700 hover:bg-slate-800',
        Yemeksepeti: 'bg-red-600 hover:bg-red-700',
        Getir: 'bg-purple-600 hover:bg-purple-700',
        Trendyol: 'bg-orange-500 hover:bg-orange-600',
        Migros: 'bg-amber-500 hover:bg-amber-600'
    };
    const platformRozetRenkleri = {
        Paket: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
        Yemeksepeti: 'bg-red-100 text-red-700 dark:bg-red-950/60 dark:text-red-300',
        Getir: 'bg-purple-100 text-purple-700 dark:bg-purple-950/60 dark:text-purple-300',
        Trendyol: 'bg-orange-100 text-orange-700 dark:bg-orange-950/60 dark:text-orange-300',
        Migros: 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300'
    };
    const toastRenkleri = {
        basari: 'bg-emerald-600',
        hata: 'bg-rose-600',
        uyari: 'bg-amber-500',
        '': 'bg-slate-900 dark:bg-slate-700'
    };

    Alpine.data('satisEkrani', () => ({
        // ---------- Durum (state) ----------
        urunler: veri.urunler,
        odemeTurleri: veri.odemeTurleri,
        ekran: veri.ekran,                 // { masalar: [...], paketler: [...] }
        platformlar: veri.platformlar,     // parası uygulamada ödenen kaynaklar
        paketKaynaklari: ['Paket', ...veri.platformlar],

        gorunum: 'plan',                   // 'plan' | 'satis'
        // kisi: adisyon açılmadan girilen kişi sayısı (ilk ürünle sunucuya gider)
        secili: { kaynak: null, masaId: null, adisyonId: null, baslik: '', kisi: null },
        adisyon: null,                     // sunucudan gelen son adisyon (yoksa null)
        yerelSatirlar: [],                 // ekranda görünen satırlar (iyimser)

        seciliKategori: null,
        arama: '',
        odemeAcik: false,
        iptalAcik: false,
        iptalNedeni: '',
        indirimAcik: false,
        indirimFormu: { yuzdeMi: true, deger: '', neden: '' },
        toast: { yazi: '', tur: '' },

        // Yükleniyor durumları (spinner)
        adisyonYukleniyor: false,
        yukleme: Promise.resolve(),
        planYukleniyor: false,
        odeniyor: null,                    // ödeme türü id'si: o düğmede spinner döner

        kuyruk: Promise.resolve(),
        bekleyenIstek: 0,
        simdi: Date.now(),

        init() {
            this.seciliKategori = this.kategoriler[0] ?? null;
            // Masa planı arada bir yenilensin (başka cihaz da kullanırsa) ve süreler ilerlesin
            setInterval(() => this.simdi = Date.now(), 30000);
            // Satış ekranındayken de yenilenir: üstteki masa şeridi güncel kalsın
            setInterval(() => { if (!this.bekleyenIstek) this.planiYenile(); }, 15000);
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

        // İndirimden önce (ekrandaki satırlardan, sunucuyu beklemeden)
        get araToplam() {
            return this.yerelSatirlar.reduce((toplam, satir) => toplam + satir.adet * satir.birimFiyat, 0);
        },

        // Sunucudaki AdisyonServisi.IndirimHesapla ile aynı kural
        get indirimTutari() {
            const indirim = this.adisyon?.indirim;
            if (!indirim || indirim.deger <= 0) return 0;
            const tutar = indirim.yuzdeMi ? Math.round(this.araToplam * indirim.deger) / 100 : indirim.deger;
            return Math.min(tutar, this.araToplam);
        },

        get toplam() { return this.araToplam - this.indirimTutari; },

        get acikAdisyonSayisi() {
            return this.ekran.masalar.filter(masa => masa.doluMu).length + this.ekran.paketler.length;
        },

        get platformMu() { return this.platformlar.includes(this.secili.kaynak); },

        get kisiSayisi() { return this.adisyon?.kisiSayisi ?? this.secili.kisi; },

        get acikToplam() {
            return this.ekran.masalar.reduce((toplam, masa) => toplam + masa.toplam, 0)
                 + this.ekran.paketler.reduce((toplam, paket) => toplam + paket.toplam, 0);
        },

        get toastRengi() { return toastRenkleri[this.toast.tur] ?? toastRenkleri['']; },

        adisyondakiAdet(urunId) {
            return this.yerelSatirlar.filter(satir => satir.urunId === urunId).reduce((adet, satir) => adet + satir.adet, 0);
        },

        masaRengi(doluMu) { return doluMu ? masaRenkleri.dolu : masaRenkleri.bos; },
        platformDugmeRengi(kaynak) { return platformDugmeRenkleri[kaynak] ?? platformDugmeRenkleri.Paket; },
        platformRozetRengi(kaynak) { return platformRozetRenkleri[kaynak] ?? platformRozetRenkleri.Paket; },

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
            this.adisyonaGec({ kaynak: 'Masa', masaId: masa.id, adisyonId: masa.adisyonId, baslik: masa.ad, kisi: null });
        },

        paketAc(kaynak) {
            this.adisyonaGec({ kaynak, masaId: null, adisyonId: null, baslik: `${kaynak} (yeni)`, kisi: null });
        },

        adisyonAc(adisyonId, baslik, kaynak) {
            this.adisyonaGec({ kaynak, masaId: null, adisyonId, baslik, kisi: null });
        },

        async adisyonaGec(yeniSecim) {
            this.secili = yeniSecim;
            // Alpine nesneyi reaktif bir kılıfa (proxy) sarar. Karşılaştırmalar (this.secili === secim)
            // doğru çalışsın diye ham nesneyi değil, kılıflı olanı tutuyoruz.
            const secim = this.secili;
            this.adisyon = null;
            this.yerelSatirlar = [];
            this.odemeAcik = this.iptalAcik = this.indirimAcik = false;
            this.arama = '';
            this.gorunum = 'satis';

            if (!secim.adisyonId) return;
            this.adisyonYukleniyor = true;
            this.yukleme = this.adisyonuGetir(secim);   // Öde/Hesap'a erken basılırsa bunu bekler
            await this.yukleme;
        },

        async adisyonuGetir(secim) {
            try {
                const cevap = await fetch(`/Satis/Adisyon/${secim.adisyonId}`);
                if (this.secili !== secim) return;   // bu arada başka masaya geçildi
                if (cevap.ok) {
                    this.adisyonuYaz(await cevap.json());
                } else {
                    this.bildir('Bu adisyon kapanmış.', 'hata');
                    secim.adisyonId = null;
                    this.planiYenile();
                }
            } catch {
                this.bildir('Bağlantı hatası. Tekrar dene.', 'hata');
            } finally {
                if (this.secili === secim) this.adisyonYukleniyor = false;
            }
        },

        geriDon() {
            if (this.odeniyor) return;
            if (this.odemeAcik || this.iptalAcik || this.indirimAcik) {
                this.odemeAcik = this.iptalAcik = this.indirimAcik = false;
                return;
            }
            this.gorunum = 'plan';
        },

        // ---------- Ürün ekle / adet / sil ----------
        urunEkle(urun) {
            // Adisyon daha yüklenmediyse dokunuşu kaybetme: yükleme bitince ekle
            // (beklemeden eklense masaya ikinci adisyon açılmaya çalışılırdı)
            if (this.adisyonYukleniyor) {
                const secim = this.secili;
                this.yukleme.then(() => { if (this.secili === secim && !this.adisyonYukleniyor) this.urunEkle(urun); });
                return;
            }
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
                adisyonId: secim.adisyonId, kaynak: secim.kaynak, masaId: secim.masaId, urunId: urun.id, adet: 1,
                kisiSayisi: secim.adisyonId ? null : secim.kisi   // önceden girilen kişi sayısı ilk ürünle gider
            }));
        },

        arttir(satir) {
            const urun = this.urunler.find(oge => oge.id === satir.urunId);
            if (urun) this.urunEkle(urun);
        },

        azalt(satir) { this.adetYaz(satir, satir.adet - 1); },

        sil(satir) { this.adetYaz(satir, 0); },   // tek dokunuşla satır gider

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

        // ---------- Kişi sayısı ----------
        // Garson önce müşteriyi oturtur, sonra sipariş alır: kişi sayısı ürün eklemeden de girilebilir.
        async kisiDegistir(fark) {
            if (this.secili.kaynak !== 'Masa') return;   // kişi sayısı sadece masada anlamlı
            if (this.adisyonYukleniyor) await this.yukleme;
            if (!this.adisyon && this.yerelSatirlar.length) await this.kuyruk;   // ilk ürün yolda: adisyon açılsın

            const yeni = Math.min(50, Math.max(1, (this.kisiSayisi ?? 0) + fark));
            if (yeni === this.kisiSayisi) return;

            if (!this.adisyon) {
                this.secili.kisi = yeni;   // adisyon yok: ilk ürünle birlikte gönderilecek
                return;
            }
            this.adisyon.kisiSayisi = yeni;
            const secim = this.secili;
            this.istekGonder('/Satis/Kisi', () => ({ adisyonId: secim.adisyonId, kisi: yeni }));
        },

        // ---------- Ödeme / iptal ----------
        // Sepet boşsa nazik uyarı; ilk ürün hâlâ yoldaysa adisyon açılana kadar bekle
        async adisyonHazirMi(bosMesaji) {
            if (this.adisyonYukleniyor) await this.yukleme;
            if (!this.yerelSatirlar.length) {
                this.bildir(bosMesaji, 'uyari');
                return false;
            }
            if (!this.adisyon || this.bekleyenIstek) await this.kuyruk;
            if (!this.adisyon) {
                this.bildir(bosMesaji, 'uyari');
                return false;
            }
            return true;
        },

        async odemeyiAc() {
            if (this.odeniyor) return;
            if (!(await this.adisyonHazirMi('Sepet boş — ödeme alınacak bir şey yok.'))) return;
            this.iptalAcik = this.indirimAcik = false;
            if (this.platformMu) {
                // Getir/Trendyol/Yemeksepeti/Migros: para uygulamada ödendi, tür sorma, kapat
                await this.ode({ id: null, ad: this.secili.kaynak });
                return;
            }
            this.odemeAcik = true;
        },

        async ode(odeme) {
            if (this.odeniyor || !this.adisyon) return;
            const secim = this.secili;
            const baslik = secim.baslik;   // cevap gelince başlık değişebilir, şimdiden sakla
            const tutar = this.toplam;
            let basariliMi = false;

            this.odeniyor = odeme.id ?? 'platform';
            await this.istekGonder('/Satis/Ode', () => ({ adisyonId: secim.adisyonId, odemeTuruId: odeme.id }), () => basariliMi = true);
            this.odeniyor = null;

            if (basariliMi) {
                this.bildir(`${baslik} kapandı · ${this.para(tutar)} · ${odeme.ad}`, 'basari');
                this.odemeAcik = false;
                if (this.secili === secim) this.gorunum = 'plan';
            }
        },

        async iptalAc() {
            if (!(await this.adisyonHazirMi('Sepet boş — iptal edilecek adisyon yok.'))) return;
            this.odemeAcik = this.indirimAcik = false;
            this.iptalNedeni = '';
            this.iptalAcik = true;
            this.$nextTick(() => this.$refs.iptalNedeni.focus());
        },

        iptalEt() {
            const neden = this.iptalNedeni.trim();
            if (!neden) {
                this.bildir('İptal nedenini yaz.', 'uyari');
                this.$refs.iptalNedeni.focus();
                return;
            }
            const secim = this.secili;
            const baslik = secim.baslik;
            this.istekGonder('/Satis/Iptal', () => ({ adisyonId: secim.adisyonId, neden }), () => {
                this.bildir(`${baslik} iptal edildi`);
            });
            this.iptalAcik = false;
            this.gorunum = 'plan';
        },

        // ---------- İndirim ----------
        async indirimAc() {
            if (!(await this.adisyonHazirMi('Sepet boş — indirim yapılacak adisyon yok.'))) return;
            this.odemeAcik = this.iptalAcik = false;
            const mevcut = this.adisyon.indirim;
            this.indirimFormu = mevcut
                ? { yuzdeMi: mevcut.yuzdeMi, deger: String(mevcut.deger).replace('.', ','), neden: mevcut.neden ?? '' }
                : { yuzdeMi: true, deger: '', neden: '' };
            this.indirimAcik = true;
            this.$nextTick(() => this.$refs.indirimDegeri.focus());
        },

        // deger: hazır düğmelerden (%10) gelir; yoksa kutuya yazılan
        indirimUygula(deger) {
            const yazi = deger ?? this.indirimFormu.deger;
            const sayi = Number(String(yazi).replace(',', '.'));
            if (!(sayi > 0)) {
                this.bildir('İndirim için bir sayı yaz.', 'uyari');
                this.$refs.indirimDegeri.focus();
                return;
            }
            const yuzdeMi = deger != null ? true : this.indirimFormu.yuzdeMi;
            const neden = this.indirimFormu.neden.trim() || null;
            const secim = this.secili;
            this.istekGonder('/Satis/Indirim', () => ({ adisyonId: secim.adisyonId, yuzdeMi, deger: sayi, neden }),
                () => this.bildir(`İndirim uygulandı · ${yuzdeMi ? '%' + sayi : this.para(sayi)}`, 'basari'));
            this.indirimAcik = false;
        },

        indirimKaldir() {
            const secim = this.secili;
            this.istekGonder('/Satis/Indirim', () => ({ adisyonId: secim.adisyonId, yuzdeMi: true, deger: 0, neden: null }),
                () => this.bildir('İndirim kaldırıldı'));
            this.indirimAcik = false;
        },

        // ---------- Sunucu ile konuşma ----------
        // govdeHazirla bir fonksiyon: istek sırası gelince çalışır, böylece önceki
        // istekte açılan adisyonun numarasını (adisyonId) kullanabilir.
        // Geriye bir Promise döner: isteyen "await" ile bitmesini bekleyebilir.
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
                    else if ('adisyon' in sonuc) secim.adisyonId = null;   // son satır silindi ya da kapandı

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
            return this.kuyruk;
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
            this.planYukleniyor = true;
            try {
                const cevap = await fetch('/Satis/Durum');
                if (cevap.ok) this.ekran = await cevap.json();
            } catch {
                // sessiz geç: 15 sn sonra yine denenecek
            } finally {
                this.planYukleniyor = false;
            }
        },

        bildir(yazi, tur = '') {
            this.toast = { yazi, tur };
            clearTimeout(this.toastZamani);
            this.toastZamani = setTimeout(() => this.toast = { yazi: '', tur: '' }, 3000);
        }
    }));
});

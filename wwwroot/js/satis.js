// Satış ekranı (POS) — Alpine.js bileşeni.
//
// Durum yönetimi (state) kuralları:
//  1) Sunucu "gerçek"tir. Her işlemden sonra sunucu güncel adisyonu ve masa planını döner.
//  2) Ekran beklemez (iyimser güncelleme): ürüne dokununca satır hemen eklenir, istek arkadan gider.
//  3) İstekler sırayla gider (kuyruk). Hızlı hızlı dokunulsa bile sıra karışmaz.
//  4) Bekleyen istek varken sunucu cevabı ekrana yazılmaz; son istek bitince yazılır.
//     Böylece "3 kere dokundum, ekranda bir an 2 göründü" gibi zıplamalar olmaz.
//
// İşlemler (İkram, Böl, İskonto, Taşı, İptal, İade, Ödeme) ortadaki tek bir pencerede açılır:
// "pencere" değişkeni hangisinin açık olduğunu tutar.

document.addEventListener('alpine:init', () => {
    const veri = JSON.parse(document.getElementById('satis-verisi').textContent);
    const anahtarDegeri = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const paraBicimi = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const tamSayiBicimi = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 });
    const saatBicimi = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

    // ---------- Renkler (Tailwind sınıfları) ----------
    const platformRenkleri = {
        Masa: 'bg-[#141414]',
        Paket: 'bg-slate-600',
        Yemeksepeti: 'bg-red-600',
        Getir: 'bg-purple-600',
        Trendyol: 'bg-orange-500',
        Migros: 'bg-amber-500'
    };
    const toastRenkleri = {
        basari: 'bg-[#141414]',
        hata: 'bg-rose-600',
        uyari: 'bg-amber-500',
        '': 'bg-[#141414]'
    };
    const pencereBasliklari = {
        odeme: 'Ödeme al',
        iptal: 'Adisyonu iptal et',
        indirim: 'İskonto',
        ikram: 'İkram',
        tasi: 'Masaya taşı',
        bol: 'Hesabı böl',
        iade: 'İade'
    };

    Alpine.data('satisEkrani', () => ({
        // ---------- Durum (state) ----------
        urunler: veri.urunler,
        odemeTurleri: veri.odemeTurleri,
        ekran: veri.ekran,                 // { masalar: [...], paketler: [...] }
        platformlar: veri.platformlar,     // parası uygulamada ödenen kaynaklar
        paketKaynaklari: ['Paket', ...veri.platformlar],
        hazirIptalNedenleri: ['Müşteri vazgeçti', 'Yanlış giriş', 'Ürün bitti', 'Çok bekledi'],

        gorunum: 'plan',                   // 'plan' | 'satis'
        planFiltresi: 'tum',               // 'tum' | 'dolu' | 'bos' | 'paket'
        // kisi: adisyon açılmadan girilen kişi sayısı (ilk ürünle sunucuya gider)
        secili: { kaynak: null, masaId: null, adisyonId: null, baslik: '', kisi: null },
        adisyon: null,                     // sunucudan gelen son adisyon (yoksa null)
        yerelSatirlar: [],                 // ekranda görünen satırlar (iyimser)

        seciliKategori: null,
        arama: '',
        menuAcik: false,
        seciliSatir: null,                 // adisyonda dokunulan satır: adet düğmeleri yalnız onda görünür
        pencere: null,                     // açık pencere: 'odeme' | 'iptal' | 'indirim' | 'ikram' | 'tasi' | 'bol' | 'iade'
        iptalNedeni: '',
        indirimFormu: { yuzdeMi: true, deger: '', neden: '' },
        bolSecimi: {},                     // satırId -> seçilen adet
        iade: { liste: [], secili: null, neden: '', yukleniyor: false, gonderiliyor: false },
        toast: { yazi: '', tur: '' },

        // Bağlantı durumu (üst çubuktaki noktalar)
        internetVar: navigator.onLine,
        sunucuVar: true,

        // Yükleniyor durumları (spinner)
        adisyonYukleniyor: false,
        yukleme: Promise.resolve(),
        planYukleniyor: false,
        odeniyor: null,                    // ödeme türü id'si: o düğmede spinner döner

        kuyruk: Promise.resolve(),
        bekleyenIstek: 0,
        ekranSurumu: 0,                    // her işlem cevabında artar (eski plan yenilemesini ayırt etmek için)
        simdi: Date.now(),

        init() {
            this.seciliKategori = this.kategoriler[0] ?? null;
            // Saat ve geçen süreler ilerlesin
            setInterval(() => this.simdi = Date.now(), 15000);
            // Masa planı arada bir yenilensin (başka cihaz da kullanırsa)
            setInterval(() => { if (!this.bekleyenIstek) this.planiYenile(); }, 15000);
            window.addEventListener('online', () => this.internetVar = true);
            window.addEventListener('offline', () => this.internetVar = false);
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

        get gorunenMasalar() {
            if (this.planFiltresi === 'dolu') return this.ekran.masalar.filter(masa => masa.doluMu);
            if (this.planFiltresi === 'bos') return this.ekran.masalar.filter(masa => !masa.doluMu);
            return this.ekran.masalar;
        },

        get planSuzgecleri() {
            const dolu = this.ekran.masalar.filter(masa => masa.doluMu).length;
            return [
                { kod: 'tum', ad: 'Tümü', sayi: this.ekran.masalar.length + this.ekran.paketler.length },
                { kod: 'dolu', ad: 'Dolu', sayi: dolu },
                { kod: 'bos', ad: 'Boş', sayi: this.ekran.masalar.length - dolu },
                { kod: 'paket', ad: 'Paketler', sayi: this.ekran.paketler.length }
            ];
        },

        // Soldaki liste: dolu masalar + paketler, en eski en üstte
        get acikListe() {
            const masalar = this.ekran.masalar.filter(masa => masa.doluMu).map(masa => ({
                anahtar: 'm' + masa.id, masa, baslik: masa.ad, kaynak: 'Masa',
                toplam: masa.toplam, acilis: masa.acilis, garson: masa.garson
            }));
            const paketler = this.ekran.paketler.map(paket => ({
                anahtar: 'p' + paket.adisyonId, adisyonId: paket.adisyonId, baslik: paket.baslik, kaynak: paket.kaynak,
                toplam: paket.toplam, acilis: paket.acilis, garson: paket.garson
            }));
            return [...masalar, ...paketler].sort((birinci, ikinci) => new Date(birinci.acilis) - new Date(ikinci.acilis));
        },

        get satirlar() { return this.yerelSatirlar; },

        // İkram ve indirimden önce (ekrandaki satırlardan, sunucuyu beklemeden)
        get araToplam() {
            return this.yerelSatirlar.reduce((toplam, satir) => toplam + satir.adet * satir.birimFiyat, 0);
        },

        get ikramTutari() {
            return this.yerelSatirlar.filter(satir => satir.ikramMi).reduce((toplam, satir) => toplam + satir.adet * satir.birimFiyat, 0);
        },

        // Sunucudaki AdisyonServisi.IndirimHesapla ile aynı kural (ikramlar hariç tutar üzerinden)
        get indirimTutari() {
            return this.indirimHesapla(this.araToplam - this.ikramTutari);
        },

        get toplam() { return this.araToplam - this.ikramTutari - this.indirimTutari; },

        get platformMu() { return this.platformlar.includes(this.secili.kaynak); },

        get kisiSayisi() { return this.adisyon?.kisiSayisi ?? this.secili.kisi; },

        get acikToplam() {
            return this.ekran.masalar.reduce((toplam, masa) => toplam + masa.toplam, 0)
                 + this.ekran.paketler.reduce((toplam, paket) => toplam + paket.toplam, 0);
        },

        get pencereBasligi() { return pencereBasliklari[this.pencere] ?? ''; },

        get saatYazisi() { return saatBicimi.format(new Date(this.simdi)); },

        get toastRengi() { return toastRenkleri[this.toast.tur] ?? toastRenkleri['']; },

        // Böl penceresinde seçilenlerin tutarı (yüzde iskonto varsa o da düşülür; sunucu da öyle yapar)
        get bolAdedi() {
            return Object.values(this.bolSecimi).reduce((toplam, adet) => toplam + adet, 0);
        },

        get bolToplami() {
            const ara = (this.adisyon?.satirlar ?? []).reduce((toplam, satir) =>
                toplam + (satir.ikramMi ? 0 : (this.bolSecimi[satir.id] ?? 0) * satir.birimFiyat), 0);
            const indirim = this.adisyon?.indirim;
            if (!indirim?.yuzdeMi) return ara;
            return ara - Math.min(Math.round(ara * indirim.deger) / 100, ara);
        },

        indirimHesapla(tutar) {
            const indirim = this.adisyon?.indirim;
            if (!indirim || indirim.deger <= 0) return 0;
            const hesap = indirim.yuzdeMi ? Math.round(tutar * indirim.deger) / 100 : indirim.deger;
            return Math.min(hesap, tutar);
        },

        adisyondakiAdet(urunId) {
            return this.yerelSatirlar.filter(satir => satir.urunId === urunId).reduce((adet, satir) => adet + satir.adet, 0);
        },

        platformDugmeRengi(kaynak) { return platformRenkleri[kaynak] ?? platformRenkleri.Paket; },

        // ---------- Biçimlendirme ----------
        // ₺370 / ₺643,50: tam sayıda kuruş yazılmaz (menü kartlarında sade dursun)
        // Eksi tutarda işaret ₺'nin önüne gelir: −₺50
        para(tutar) {
            const mutlak = Math.abs(tutar);
            const bicim = Number.isInteger(Math.round(mutlak * 100) / 100) ? tamSayiBicimi : paraBicimi;
            return (tutar < 0 ? '−' : '') + '₺' + bicim.format(mutlak);
        },
        saat(tarih) { return saatBicimi.format(new Date(tarih)); },
        // Ürünün görseli: önce dükkânın Menü'den yüklediği foto; yoksa hazır gerçek fotoğraf
        // (wwwroot/img/urun, kaynakları KAYNAKLAR.md'de); o da yoksa çizim (wwwroot/img/ornek).
        gorsel(urun) {
            if (urun.foto) return urun.foto;
            const kategori = urun.kategori.toLocaleLowerCase('tr-TR');
            const ad = urun.ad.toLocaleLowerCase('tr-TR');
            const foto = dosya => `/img/urun/${dosya}`;
            const cizim = dosya => `/img/ornek/${dosya}.svg`;

            // Dükkânın kendi menü görselinden kesilen fotoğraflar (ürün adıyla birebir)
            const menudenFoto = {
                'original smash menü': 'menu-original-smash.jpg',
                'classic zip menü': 'menu-classic-zip.jpg',
                'big zip menü': 'menu-big-zip.jpg',
                'crispy chicken menü': 'menu-crispy-chicken.jpg',
                'ayvalık tost': 'tost-ayvalik.jpg',
                'kumru tost': 'tost-kumru.jpg',
                'patso': 'tost-patso.jpg',
                'sanayi tost': 'tost-sanayi.jpg',
                'karışık bazlama': 'bazlama-karisik.jpg',
                'karışık tost': 'tost-karisik.jpg',
                'kavurma kaşarlı tost': 'tost-kavurma-kasarli.jpg'
            };
            if (menudenFoto[ad]) return foto(menudenFoto[ad]);

            if (kategori.includes('içecek')) {
                if (ad.includes('zero')) return foto('icecek-kola-zero.png');
                if (ad.includes('kola')) return foto('icecek-kola.webp');
                if (ad.includes('fanta')) return foto('icecek-fanta.png');
                if (ad.includes('sprite')) return foto('icecek-sprite.png');
                if (ad.includes('gazoz')) return foto('icecek-gazoz.webp');
                if (ad.includes('ayran')) return foto('ayran.jpg');
                if (ad.includes('soda')) return foto('icecek-soda.png');
                if (ad.includes('limonata')) return foto('icecek-limonata.png');
                // Türkçe küçük harfte "Ice" → "ıce" olur: ikisini de tanı
                if (ad.includes('ice tea') || ad.includes('ıce tea')) return foto('icecek-ice-tea.png');
                if (ad.includes('enerji')) return foto('icecek-enerji.png');
                if (ad === 'su' || ad.startsWith('su ')) return foto('icecek-su.png');
                return cizim('icecek');
            }
            if (kategori.includes('smash')) return foto('smash-burger.jpg');
            if (kategori.includes('burger')) return foto('classic-burger.jpg');
            if (kategori.includes('atıştırmalık')) {
                if (ad.includes('cheddar')) return foto('cheddarli-patates.jpg');
                if (ad.includes('soğan')) return foto('sogan-halkasi.jpg');
                if (ad.includes('tavuk')) return foto('citir-tavuk.jpg');
                if (ad.includes('mozzarella')) return foto('mozzarella.jpg');
                if (ad.includes('patates')) return foto('patates.jpg');
                return cizim('patates');
            }
            if (kategori === 'sos') return cizim('sos');
            if (kategori.includes('tatlı')) return foto('tatli.jpg');
            if (kategori.includes('bazlama')) return foto('bazlama.jpg');
            if (kategori.includes('patso')) return foto('patso.jpg');
            if (kategori.includes('pattis')) return cizim('patso');
            if (kategori.includes('yumurta') || ad.includes('yumurta')) return foto('yumurtali-tost.jpg');
            return foto('tost.jpg');
        },
        basHarf(ad) { return (ad ?? '?').trim().charAt(0).toLocaleUpperCase('tr-TR'); },
        // "İç 2" -> "İ2", "Getir 14:05" -> "G": soldaki listedeki küçük kare
        kisaAd(baslik) {
            const parcalar = (baslik ?? '').split(' ');
            const sayi = parcalar.find(parca => /^\d+$/.test(parca)) ?? '';
            return this.basHarf(baslik) + sayi;
        },
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

        acikListeAc(oge) {
            if (oge.masa) this.masaAc(oge.masa);
            else this.adisyonAc(oge.adisyonId, oge.baslik, oge.kaynak);
        },

        async adisyonaGec(yeniSecim) {
            this.secili = yeniSecim;
            // Alpine nesneyi reaktif bir kılıfa (proxy) sarar. Karşılaştırmalar (this.secili === secim)
            // doğru çalışsın diye ham nesneyi değil, kılıflı olanı tutuyoruz.
            const secim = this.secili;
            this.adisyon = null;
            this.yerelSatirlar = [];
            this.pencere = null;
            this.seciliSatir = null;
            this.arama = '';
            this.gorunum = 'satis';

            if (!secim.adisyonId) return;
            this.adisyonYukleniyor = true;
            this.yukleme = this.adisyonuGetir(secim);   // Öde'ye erken basılırsa bunu bekler
            await this.yukleme;
        },

        async adisyonuGetir(secim) {
            try {
                const cevap = await fetch(`/Satis/Adisyon/${secim.adisyonId}`);
                this.sunucuVar = true;
                if (this.secili !== secim) return;   // bu arada başka masaya geçildi
                if (cevap.ok) {
                    this.adisyonuYaz(await cevap.json());
                } else {
                    this.bildir('Bu adisyon kapanmış.', 'hata');
                    secim.adisyonId = null;
                    this.planiYenile();
                }
            } catch {
                this.sunucuVar = false;
                this.bildir('Bağlantı hatası. Tekrar dene.', 'hata');
            } finally {
                if (this.secili === secim) this.adisyonYukleniyor = false;
            }
        },

        geriDon() {
            if (this.odeniyor) return;
            if (this.menuAcik) { this.menuAcik = false; return; }
            if (this.pencere) { this.pencereyiKapat(); return; }
            this.gorunum = 'plan';
        },

        pencereyiKapat() {
            if (this.odeniyor) return;
            this.pencere = null;
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
            // 1) Ekranı hemen güncelle (ikram satırına eklenmez: sunucu da öyle yapar)
            const satir = this.yerelSatirlar.find(oge => oge.urunId === urun.id && oge.birimFiyat === urun.fiyat && !oge.ikramMi);
            if (satir) {
                satir.adet++;
            } else {
                this.yerelSatirlar.push({ anahtar: 'yeni-' + urun.id, id: null, urunId: urun.id, ad: urun.ad, adet: 1, birimFiyat: urun.fiyat, ikramMi: false });
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

        // ---------- Pencere açmadan önce ----------
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

        // ---------- Ödeme ----------
        async odemeyiAc() {
            if (this.odeniyor) return;
            if (!(await this.adisyonHazirMi('Adisyon boş — ödeme alınacak bir şey yok.'))) return;
            if (this.platformMu) {
                // Getir/Trendyol/Yemeksepeti/Migros: para uygulamada ödendi, tür sorma, kapat
                await this.ode({ id: null, ad: this.secili.kaynak });
                return;
            }
            this.pencere = 'odeme';
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
                this.pencere = null;
                if (this.secili === secim) this.gorunum = 'plan';
            }
        },

        // ---------- İptal ----------
        async iptalAc() {
            if (!(await this.adisyonHazirMi('Adisyon boş — iptal edilecek bir şey yok.'))) return;
            this.iptalNedeni = '';
            this.pencere = 'iptal';
            this.$nextTick(() => this.$refs.iptalNedeni.focus());
        },

        iptalEt() {
            const neden = this.iptalNedeni.trim();
            if (!neden) {
                this.bildir('İptal nedenini yaz ya da seç.', 'uyari');
                this.$refs.iptalNedeni.focus();
                return;
            }
            const secim = this.secili;
            const baslik = secim.baslik;
            this.istekGonder('/Satis/Iptal', () => ({ adisyonId: secim.adisyonId, neden }), () => {
                this.bildir(`${baslik} iptal edildi`);
            });
            this.pencere = null;
            this.gorunum = 'plan';
        },

        // ---------- İskonto (indirim) ----------
        async indirimAc() {
            if (this.platformMu) return;
            if (!(await this.adisyonHazirMi('Adisyon boş — iskonto yapılacak bir şey yok.'))) return;
            const mevcut = this.adisyon.indirim;
            this.indirimFormu = mevcut
                ? { yuzdeMi: mevcut.yuzdeMi, deger: String(mevcut.deger).replace('.', ','), neden: mevcut.neden ?? '' }
                : { yuzdeMi: true, deger: '', neden: '' };
            this.pencere = 'indirim';
            this.$nextTick(() => this.$refs.indirimDegeri.focus());
        },

        // deger: hazır düğmelerden (%10) gelir; yoksa kutuya yazılan
        indirimUygula(deger) {
            const yazi = deger ?? this.indirimFormu.deger;
            const sayi = Number(String(yazi).replace(',', '.'));
            if (!(sayi > 0)) {
                this.bildir('İskonto için bir sayı yaz.', 'uyari');
                this.$refs.indirimDegeri.focus();
                return;
            }
            const yuzdeMi = deger != null ? true : this.indirimFormu.yuzdeMi;
            const neden = this.indirimFormu.neden.trim() || null;
            const secim = this.secili;
            this.istekGonder('/Satis/Indirim', () => ({ adisyonId: secim.adisyonId, yuzdeMi, deger: sayi, neden }),
                () => this.bildir(`İskonto uygulandı · ${yuzdeMi ? '%' + sayi : this.para(sayi)}`, 'basari'));
            this.pencere = null;
        },

        indirimKaldir() {
            const secim = this.secili;
            this.istekGonder('/Satis/Indirim', () => ({ adisyonId: secim.adisyonId, yuzdeMi: true, deger: 0, neden: null }),
                () => this.bildir('İskonto kaldırıldı'));
            this.pencere = null;
        },

        // ---------- İkram ----------
        async ikramAc() {
            if (this.platformMu) return;
            if (!(await this.adisyonHazirMi('Adisyon boş — ikram edilecek ürün yok.'))) return;
            this.pencere = 'ikram';
        },

        // adet: null = satırın tamamı (ikramsa geri alır)
        ikramEt(satir, adet) {
            const secim = this.secili;
            const geriAlMi = satir.ikramMi;
            this.istekGonder('/Satis/Ikram', () => ({ adisyonId: secim.adisyonId, satirId: satir.id, adet }),
                () => this.bildir(geriAlMi ? `${satir.ad} ikramı geri alındı` : `${satir.ad} ikram edildi`, geriAlMi ? '' : 'basari'));
        },

        // ---------- Taşı ----------
        async tasiAc() {
            if (this.platformMu) return;
            if (!(await this.adisyonHazirMi('Adisyon boş — taşınacak bir şey yok.'))) return;
            this.pencere = 'tasi';
        },

        tasi(hedef) {
            const secim = this.secili;
            const eskiBaslik = secim.baslik;
            this.istekGonder('/Satis/Tasi', () => ({ adisyonId: secim.adisyonId, hedefMasaId: hedef.id }),
                () => this.bildir(hedef.doluMu ? `${eskiBaslik}, ${hedef.ad} ile birleşti` : `${eskiBaslik} → ${hedef.ad} taşındı`, 'basari'));
            this.pencere = null;
        },

        // ---------- Böl ----------
        async bolAc() {
            if (this.platformMu) return;
            if (!(await this.adisyonHazirMi('Adisyon boş — bölünecek bir şey yok.'))) return;
            this.bolSecimi = {};
            this.pencere = 'bol';
        },

        bolDegistir(satir, fark) {
            const yeni = Math.min(satir.adet, Math.max(0, (this.bolSecimi[satir.id] ?? 0) + fark));
            this.bolSecimi = { ...this.bolSecimi, [satir.id]: yeni };
        },

        bolHepsi() {
            if (this.bolAdedi) { this.bolSecimi = {}; return; }
            this.bolSecimi = Object.fromEntries(this.adisyon.satirlar.map(satir => [satir.id, satir.adet]));
        },

        async bolOde(odeme) {
            if (this.odeniyor || !this.bolAdedi) return;
            const secim = this.secili;
            const tutar = this.bolToplami;
            const secimler = Object.entries(this.bolSecimi)
                .filter(([, adet]) => adet > 0)
                .map(([satirId, adet]) => ({ satirId: Number(satirId), adet }));
            let basariliMi = false;

            this.odeniyor = odeme.id;
            await this.istekGonder('/Satis/Bol', () => ({ adisyonId: secim.adisyonId, secimler, odemeTuruId: odeme.id }), () => basariliMi = true);
            this.odeniyor = null;

            if (basariliMi) {
                this.bildir(`${this.para(tutar)} ${odeme.ad} ile ödendi`, 'basari');
                this.pencere = null;
                // Hepsi ödendiyse masa kapandı: plana dön
                if (this.secili === secim && !this.adisyon) this.gorunum = 'plan';
            }
        },

        // ---------- İade ----------
        // Açık adisyonla ilgisi yok: bugün ödenmiş bir adisyonun parası geri verilir.
        // Bu yüzden kuyruğa girmez, ekrandaki adisyonu değiştirmez.
        async iadeAc() {
            this.iade = { liste: [], secili: null, neden: '', yukleniyor: true, gonderiliyor: false };
            this.pencere = 'iade';
            try {
                const cevap = await fetch('/Satis/Odenenler');
                this.sunucuVar = true;
                if (cevap.ok) this.iade.liste = await cevap.json();
            } catch {
                this.sunucuVar = false;
                this.bildir('Bağlantı hatası. Tekrar dene.', 'hata');
            } finally {
                this.iade.yukleniyor = false;
            }
        },

        async iadeEt() {
            const kayit = this.iade.secili;
            const neden = this.iade.neden.trim();
            if (!kayit) return;
            if (!neden) {
                this.bildir('İade nedenini yaz.', 'uyari');
                this.$refs.iadeNedeni.focus();
                return;
            }
            this.iade.gonderiliyor = true;
            const { tamam, sonuc } = await this.gonder('/Satis/Iade', { adisyonId: kayit.id, neden });
            this.iade.gonderiliyor = false;
            if (!tamam) {
                this.bildir(sonuc.hata || 'İade yapılamadı.', 'hata');
                return;
            }
            this.bildir(`#${kayit.id} ${kayit.baslik} iade edildi · ${this.para(kayit.toplam)}`, 'basari');
            this.pencere = null;
        },

        // ---------- Sunucu ile konuşma ----------
        // Tek bir POST: cevabı ve masa planını işler. Hata fırlatmaz, sonucu döner.
        async gonder(adres, govde) {
            try {
                const cevap = await fetch(adres, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': anahtarDegeri },
                    body: JSON.stringify(govde)
                });
                this.sunucuVar = true;
                const sonuc = await cevap.json().catch(() => ({}));
                if (sonuc.ekran) {
                    this.ekran = sonuc.ekran;
                    this.ekranSurumu++;   // yolda olan eski bir plan yenilemesi bunu ezmesin
                }
                return { tamam: cevap.ok, sonuc };
            } catch {
                this.sunucuVar = false;
                return { tamam: false, sonuc: { hata: 'Bağlantı hatası. Tekrar dene.' } };
            }
        },

        // govdeHazirla bir fonksiyon: istek sırası gelince çalışır, böylece önceki
        // istekte açılan adisyonun numarasını (adisyonId) kullanabilir.
        // Geriye bir Promise döner: isteyen "await" ile bitmesini bekleyebilir.
        istekGonder(adres, govdeHazirla, basarili) {
            this.bekleyenIstek++;
            const secim = this.secili;

            this.kuyruk = this.kuyruk.then(async () => {
                try {
                    const { tamam, sonuc } = await this.gonder(adres, govdeHazirla());

                    if (!tamam) {
                        this.bildir(sonuc.hata || 'İşlem yapılamadı.', 'hata');
                        if (this.secili === secim) await this.adisyonuYenile();   // ekranı sunucuya eşitle
                        return;
                    }

                    // Adisyon açıldıysa (ya da taşınıp birleştiyse) numarasını hemen sakla: sıradaki istekler bunu kullanır
                    if (sonuc.adisyon) secim.adisyonId = sonuc.adisyon.id;
                    else if ('adisyon' in sonuc) secim.adisyonId = null;   // son satır silindi ya da kapandı

                    if (this.secili === secim && this.bekleyenIstek === 1) {
                        this.adisyonuYaz(sonuc.adisyon);
                    }
                    basarili?.();
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
            // Taşı'dan sonra masa / kaynak değişmiş olabilir
            this.secili.adisyonId = adisyon.id;
            this.secili.baslik = adisyon.baslik;
            this.secili.masaId = adisyon.masaId;
            this.secili.kaynak = adisyon.kaynak;
            this.yerelSatirlar = adisyon.satirlar.map(satir => ({ ...satir, anahtar: 'satir-' + satir.id }));
        },

        async adisyonuYenile() {
            if (!this.secili.adisyonId) { this.adisyonuYaz(null); return; }
            try {
                const cevap = await fetch(`/Satis/Adisyon/${this.secili.adisyonId}`);
                this.adisyonuYaz(cevap.ok ? await cevap.json() : null);
            } catch {
                this.sunucuVar = false;
            }
        },

        async planiYenile() {
            this.planYukleniyor = true;
            const surum = this.ekranSurumu;
            try {
                const cevap = await fetch('/Satis/Durum');
                this.sunucuVar = cevap.ok;
                const yeniEkran = cevap.ok ? await cevap.json() : null;
                // Bu istek yoldayken bir işlem daha yeni planı getirdiyse eskisini yazma
                if (yeniEkran && surum === this.ekranSurumu && !this.bekleyenIstek) this.ekran = yeniEkran;
            } catch {
                this.sunucuVar = false;   // 15 sn sonra yine denenecek
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

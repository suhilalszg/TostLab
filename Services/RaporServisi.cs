using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Sadece OKUR, hiçbir şeyi değiştirmez. Adisyon paneli ve aylık e-posta buradan beslenir.
// Not: SQLite ondalık (decimal) sayıları SQL içinde toplayamaz. Bir günün kaydı az olduğu için
// satırları çekip toplamı C# tarafında (LINQ to Objects) alıyoruz.
public class RaporServisi
{
    private readonly TostLabDbContext veritabani;

    public RaporServisi(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    // ===================== ADİSYON PANELİ (gün / hafta / ay) =====================

    public async Task<AdisyonPaneli> AdisyonPaneliAsync(PanelFiltresi filtre)
    {
        DateTime bas = filtre.Baslangic;
        DateTime son = filtre.Bitis;

        // Rakamlar ve kırılımlar: dönemin tamamı
        var (rapor, adisyonlar) = await AralikRaporuVeAdisyonlarAsync(bas, son, enCokSatanSayisi: 10);
        var odenenler = adisyonlar.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();
        var odemeTurleri = await veritabani.OdemeTurleri.AsNoTracking().OrderBy(odeme => odeme.Id).ToListAsync();

        // Bütün adisyonlar bir kez çekilir; liste süzgeci, iptaller ve açık adisyonlar bundan ayrılır
        var tumKayitlar = await KayitListesiAsync(bas, son);
        string? odemeSuzgeci = odemeTurleri.FirstOrDefault(odeme => odeme.Id == filtre.OdemeTuruId)?.Ad;
        var kayitlar = tumKayitlar
            .Where(kayit => filtre.Durum == null || kayit.Durum == filtre.Durum)
            .Where(kayit => string.IsNullOrWhiteSpace(filtre.Kaynak) || kayit.Kaynak == filtre.Kaynak)
            .Where(kayit => filtre.OdemeTuruId == null || kayit.OdemeTuru == odemeSuzgeci)
            .ToList();

        // Ürün satırları (ödenen adisyonlar): kategori, maliyet, ikram ve ürün performansı bundan
        var satirlar = await SatilanSatirlarAsync(bas, son);

        // Günlük çubuklar: dönemin her günü (satış olmayan gün 0)
        var gunler = new List<GunlukSatir>();
        for (DateTime gun = bas; gun < son; gun = gun.AddDays(1))
        {
            var gununkiler = odenenler.Where(adisyon => adisyon.Tarih.Date == gun).ToList();
            gunler.Add(new GunlukSatir(gun, gununkiler.Count, gununkiler.Sum(adisyon => adisyon.Toplam),
                gununkiler.Sum(adisyon => adisyon.Komisyon)));
        }

        // ---- 1) Kasa: her gün nakit satış, kasa çıkışı ve (girildiyse) sayım
        var giderler = await veritabani.KasaHareketleri.AsNoTracking()
            .Include(gider => gider.Kullanici)
            .Where(gider => gider.Tarih >= bas && gider.Tarih < son)
            .OrderBy(gider => gider.Tarih)
            .ToListAsync();
        var sayimlar = await veritabani.KasaSayimlari.AsNoTracking()
            .Where(sayim => sayim.Gun >= bas && sayim.Gun < son)
            .ToListAsync();
        var kasaGunleri = gunler
            .Where(gun => gun.Gun <= DateTime.Today)
            .Select(gun =>
            {
                var sayim = sayimlar.FirstOrDefault(kayit => kayit.Gun == gun.Gun);
                return new KasaGunu(gun.Gun,
                    odenenler.Where(adisyon => adisyon.Tarih.Date == gun.Gun && adisyon.OdemeTuru == "Nakit").Sum(adisyon => adisyon.Toplam),
                    giderler.Where(gider => gider.Tarih.Date == gun.Gun).Sum(gider => gider.Tutar),
                    sayim?.AcilisNakdi, sayim?.SayilanNakit);
            })
            .ToList();

        // ---- 2) Fire, iptal, ikram
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= bas && iz.Saat < son);
        var ikramlar = satirlar
            .Where(satir => satir.IkramMi)
            .GroupBy(satir => satir.Ad)
            .Select(grup => new IkramUrunu(grup.Key, grup.Sum(satir => satir.Adet),
                grup.Sum(satir => satir.Adet * satir.BirimFiyat), grup.Sum(satir => satir.Adet * satir.Maliyet)))
            .OrderByDescending(ikram => ikram.Tutar)
            .ToList();
        var indirimler = await IndirimleriGetirAsync(bas, son);

        // ---- 3) Operasyon: masa devri ve ürün performansı
        var masaDevri = await MasaDevriAsync(bas, son, gunler.Count(gun => gun.Adet > 0));
        var urunler = await UrunPerformansiAsync(satirlar);

        // ---- 4) Büyüme: önceki dönem ve geçen yılın aynı dönemi (bitmemiş dönemde aynı noktaya kadar)
        DateTime oncekiBas = filtre.Donem == Donem.Ay ? bas.AddMonths(-1) : bas - (son - bas);
        var oncekiOdenenler = await OdenenToplamlarAsync(oncekiBas, bas);
        var gecenYilOdenenler = await OdenenToplamlarAsync(bas.AddYears(-1), son.AddYears(-1));
        int musteri = odenenler.Sum(adisyon => adisyon.KisiSayisi ?? 1);
        string oncekiAdi = filtre.Donem switch { Donem.Hafta => "Geçen hafta", Donem.Ay => "Geçen ay", _ => "Önceki gün" };
        var oncekiKiyas = KiyasYap(oncekiAdi, rapor.Ozet, musteri, oncekiOdenenler, bas, son, oncekiBas, bas);
        var gecenYilKiyas = KiyasYap("Geçen yıl aynı dönem", rapor.Ozet, musteri, gecenYilOdenenler,
            bas, son, bas.AddYears(-1), son.AddYears(-1));

        // Isı haritası: haftanın günü x saat, iptaller hariç adisyon sayısı
        var isiHaritasi = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        foreach (var adisyon in adisyonlar.Where(adisyon => adisyon.Durum != AdisyonDurumu.Iptal))
        {
            isiHaritasi[((int)adisyon.Tarih.DayOfWeek + 6) % 7][adisyon.Tarih.Hour]++;
        }

        // Kategori yığını: her gün kategorilerin cirosu (en büyük 5 kategori, gerisi "Diğer")
        var buyukKategoriler = satirlar.GroupBy(satir => satir.Kategori)
            .OrderByDescending(grup => grup.Sum(satir => satir.NetTutar)).Take(5).Select(grup => grup.Key).ToHashSet();
        var kategoriYigini = gunler
            .Select(gun => new KategoriDilimi(gun.Gun, satirlar
                .Where(satir => satir.Tarih.Date == gun.Gun && !satir.IkramMi)
                .GroupBy(satir => buyukKategoriler.Contains(satir.Kategori) ? satir.Kategori : "Diğer")
                .ToDictionary(grup => grup.Key, grup => Math.Round(grup.Sum(satir => satir.NetTutar), 2))))
            .ToList();

        // Aylık hedef: seçilen tarihin ayı
        DateTime ayBasi = new(filtre.SecilenTarih.Year, filtre.SecilenTarih.Month, 1);
        var hedef = await veritabani.AylikHedefler.AsNoTracking()
            .FirstOrDefaultAsync(kayit => kayit.Yil == ayBasi.Year && kayit.Ay == ayBasi.Month);
        decimal ayCirosu = filtre.Donem == Donem.Ay
            ? rapor.Ozet.Ciro
            : (await OdenenToplamlarAsync(ayBasi, ayBasi.AddMonths(1))).Sum(adisyon => adisyon.Toplam);

        return new AdisyonPaneli(filtre, rapor,
            new KasaDurumu(kasaGunleri),
            tumKayitlar.Where(kayit => kayit.Durum == AdisyonDurumu.Iptal).ToList(), silinenler, ikramlar, indirimler,
            tumKayitlar.Where(kayit => kayit.Durum == AdisyonDurumu.Acik).ToList(),
            masaDevri, urunler,
            oncekiKiyas, gecenYilKiyas, giderler, isiHaritasi, kategoriYigini,
            new HedefDurumu(ayBasi.Year, ayBasi.Month, hedef?.Tutar, ayCirosu),
            gunler, kayitlar, odemeTurleri);
    }

    // ---------- Adisyon paneli yardımcıları ----------

    // Ödenen adisyonların satırları. NetTutar: iskonto satırlara tutarları oranında dağıtılmış hali (ikram 0).
    private record SatilanSatir(DateTime Tarih, int UrunId, string Ad, string Kategori, int Adet, decimal BirimFiyat,
        bool IkramMi, decimal Maliyet, decimal IskontoOrani)
    {
        public decimal NetTutar => IkramMi ? 0 : Adet * BirimFiyat * IskontoOrani;
    }

    private async Task<List<SatilanSatir>> SatilanSatirlarAsync(DateTime bas, DateTime son)
    {
        var ham = await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => satir.Siparis.Durum == AdisyonDurumu.Odendi && satir.Siparis.Tarih >= bas && satir.Siparis.Tarih < son)
            .Select(satir => new
            {
                satir.Siparis.Tarih,
                satir.UrunId,
                satir.Urun.Ad,
                satir.Urun.Kategori,
                satir.Adet,
                satir.BirimFiyat,
                satir.IkramMi,
                satir.Urun.Maliyet,
                AdisyonToplami = satir.Siparis.Toplam,
                satir.Siparis.IndirimTutari
            })
            .ToListAsync();
        return ham.Select(satir => new SatilanSatir(satir.Tarih, satir.UrunId, satir.Ad, satir.Kategori, satir.Adet,
                satir.BirimFiyat, satir.IkramMi, satir.Maliyet,
                IskontoSonrasiOrani(satir.AdisyonToplami, satir.IndirimTutari)))
            .ToList();
    }

    private async Task<List<IndirimSatiri>> IndirimleriGetirAsync(DateTime bas, DateTime son)
    {
        var ham = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi && siparis.IndirimTutari > 0
                              && siparis.Tarih >= bas && siparis.Tarih < son)
            .OrderBy(siparis => siparis.Tarih)
            .Select(siparis => new
            {
                siparis.Id,
                MasaAdi = siparis.Masa != null ? siparis.Masa.Ad : null,
                siparis.Kaynak,
                siparis.Tarih,
                siparis.Toplam,
                siparis.IndirimTutari,
                siparis.IndirimNedeni,
                Yapan = siparis.IndirimYapanKullanici != null ? siparis.IndirimYapanKullanici.KullaniciAdi : null
            })
            .ToListAsync();
        return ham.Select(siparis => new IndirimSatiri(siparis.Id,
                siparis.MasaAdi ?? AdisyonServisi.PaketBasligi(siparis.Kaynak, siparis.Tarih), siparis.Tarih,
                siparis.Toplam + siparis.IndirimTutari, siparis.IndirimTutari, siparis.IndirimNedeni, siparis.Yapan))
            .ToList();
    }

    // Masa devri: ödenen masa adisyonlarının oturma süresi; günlük devir = adisyon / masa / satışlı gün
    private async Task<MasaDevri> MasaDevriAsync(DateTime bas, DateTime son, int satisliGun)
    {
        var masaAdisyonlari = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi && siparis.Masa != null && siparis.KapanisSaati != null
                              && siparis.Tarih >= bas && siparis.Tarih < son)
            .Select(siparis => new { MasaAdi = siparis.Masa!.Ad, siparis.Tarih, Kapanis = siparis.KapanisSaati!.Value })
            .ToListAsync();
        int masaSayisi = await veritabani.Masalar.CountAsync(masa => masa.AktifMi);

        var masalar = masaAdisyonlari
            .GroupBy(adisyon => adisyon.MasaAdi)
            .Select(grup => new MasaSatiri(grup.Key, grup.Count(), grup.Average(adisyon => (adisyon.Kapanis - adisyon.Tarih).TotalMinutes)))
            .OrderBy(masa => masa.Ad)
            .ToList();
        double ortalama = masaAdisyonlari.Count == 0 ? 0 : masaAdisyonlari.Average(adisyon => (adisyon.Kapanis - adisyon.Tarih).TotalMinutes);
        decimal gunlukDevir = masaSayisi == 0 || satisliGun == 0 ? 0 : (decimal)masaAdisyonlari.Count / masaSayisi / satisliGun;
        return new MasaDevri(masaAdisyonlari.Count, ortalama, gunlukDevir, masalar);
    }

    // Ürün performansı: dönemde satılanlar + satışta olup hiç satılmayanlar (en az satanlar için)
    private async Task<List<UrunPerformansi>> UrunPerformansiAsync(List<SatilanSatir> satirlar)
    {
        var tumUrunler = await veritabani.Urunler.AsNoTracking()
            .Select(urun => new { urun.Id, urun.Ad, urun.Kategori, urun.Maliyet, urun.SatistaMi })
            .ToListAsync();
        var satilanlar = satirlar
            .Where(satir => !satir.IkramMi)
            .GroupBy(satir => satir.UrunId)
            .ToDictionary(grup => grup.Key, grup => (Adet: grup.Sum(satir => satir.Adet), Ciro: grup.Sum(satir => satir.NetTutar)));
        return tumUrunler
            .Where(urun => urun.SatistaMi || satilanlar.ContainsKey(urun.Id))
            .Select(urun =>
            {
                var (adet, ciro) = satilanlar.GetValueOrDefault(urun.Id);
                return new UrunPerformansi(urun.Id, urun.Ad, urun.Kategori, adet, Math.Round(ciro, 2), urun.Maliyet, urun.SatistaMi);
            })
            .ToList();
    }

    private record OdenenToplam(DateTime Tarih, decimal Toplam, int Kisi, decimal Komisyon);

    private async Task<List<OdenenToplam>> OdenenToplamlarAsync(DateTime bas, DateTime son)
    {
        var ham = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi && siparis.Tarih >= bas && siparis.Tarih < son)
            .Select(siparis => new { siparis.Tarih, siparis.Toplam, siparis.KisiSayisi, siparis.KomisyonOrani })
            .ToListAsync();
        return ham.Select(siparis => new OdenenToplam(siparis.Tarih, siparis.Toplam, siparis.KisiSayisi ?? 1,
            Math.Round(siparis.Toplam * siparis.KomisyonOrani / 100, 2))).ToList();
    }

    // Kıyas dönemi, şu anki dönem bitmediyse aynı noktaya kadar sayılır (OncekiCiroAsync ile aynı kural)
    private static Kiyas KiyasYap(string ad, GunOzeti ozet, int musteri, List<OdenenToplam> kiyasOdenenler,
        DateTime bas, DateTime son, DateTime kiyasBas, DateTime kiyasSon)
    {
        DateTime sinir = KiyasSiniri(bas, son, kiyasBas, kiyasSon);
        var sayilanlar = kiyasOdenenler.Where(adisyon => adisyon.Tarih < sinir).ToList();
        decimal kiyasCiro = sayilanlar.Sum(adisyon => adisyon.Toplam);
        return new Kiyas(ad, ozet.Ciro, musteri, kiyasCiro, sayilanlar.Sum(adisyon => adisyon.Kisi),
            ozet.OdenenSayisi, sayilanlar.Count, ozet.NetKazanc, kiyasCiro - sayilanlar.Sum(adisyon => adisyon.Komisyon));
    }

    private static DateTime KiyasSiniri(DateTime bas, DateTime son, DateTime kiyasBas, DateTime kiyasSon)
    {
        DateTime simdi = DateTime.Now;
        if (simdi < son)
        {
            DateTime ayniNokta = kiyasBas + (simdi - bas);
            if (ayniNokta < kiyasSon)
            {
                return ayniNokta;
            }
        }
        return kiyasSon;
    }

    // Dönemin bütün adisyonları (yeniden eskiye)
    private async Task<List<KayitSatiri>> KayitListesiAsync(DateTime bas, DateTime son)
    {
        // Adisyon hangi gün açıldıysa o güne sayılır (dükkan 12'de kapanıyor)
        var hamKayitlar = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Tarih >= bas && siparis.Tarih < son)
            .OrderByDescending(siparis => siparis.Tarih)
            .Select(siparis => new
            {
                siparis.Id,
                MasaAdi = siparis.Masa != null ? siparis.Masa.Ad : null,
                siparis.Kaynak,
                siparis.Durum,
                siparis.Tarih,
                siparis.KapanisSaati,
                OdemeTuru = siparis.OdemeTuru != null ? siparis.OdemeTuru.Ad : null,
                UrunAdedi = siparis.Satirlar.Sum(satir => satir.Adet),
                siparis.Toplam
            })
            .ToListAsync();

        return hamKayitlar
            .Select(kayit => new KayitSatiri(
                kayit.Id,
                kayit.MasaAdi ?? AdisyonServisi.PaketBasligi(kayit.Kaynak, kayit.Tarih),
                kayit.Kaynak, kayit.Durum, kayit.Tarih, kayit.KapanisSaati,
                kayit.OdemeTuru, kayit.UrunAdedi, kayit.Toplam))
            .ToList();
    }

    public async Task<AdisyonDetayi?> AdisyonDetayiAsync(int adisyonId)
    {
        var adisyon = await veritabani.Siparisler.AsNoTracking()
            .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
            .Include(siparis => siparis.Masa)
            .Include(siparis => siparis.OdemeTuru)
            .Include(siparis => siparis.IptalEdenKullanici)
            .Include(siparis => siparis.IndirimYapanKullanici)
            .Include(siparis => siparis.IadeEdenKullanici)
            .FirstOrDefaultAsync(siparis => siparis.Id == adisyonId);
        if (adisyon == null)
        {
            return null;
        }

        var satirlar = adisyon.Satirlar
            .OrderBy(satir => satir.Id)
            .Select(satir => new SatirBilgisi(satir.Id, satir.UrunId, satir.Urun.Ad, satir.Adet, satir.BirimFiyat, satir.Tutar, satir.IkramMi))
            .ToList();

        return new AdisyonDetayi(
            adisyon.Id,
            adisyon.Masa?.Ad ?? AdisyonServisi.PaketBasligi(adisyon.Kaynak, adisyon.Tarih),
            adisyon.Kaynak,
            adisyon.Durum,
            adisyon.Tarih,
            adisyon.KapanisSaati,
            adisyon.OdemeTuru?.Ad,
            adisyon.KisiSayisi,
            adisyon.Toplam,
            satirlar,
            adisyon.IptalNedeni,
            adisyon.IptalEdenKullanici?.KullaniciAdi,
            await SilinenleriGetirAsync(iz => iz.SiparisId == adisyonId),
            adisyon.IndirimTutari,
            FisServisi.IndirimAciklamasi(adisyon),
            adisyon.IndirimYapanKullanici?.KullaniciAdi,
            adisyon.IadeNedeni,
            adisyon.IadeEdenKullanici?.KullaniciAdi,
            adisyon.IadeSaati);
    }

    // İskonto sonrası kalan pay: 100 ₺'lik adisyona 10 ₺ iskonto -> her satır 0,9 ile çarpılır
    private static decimal IskontoSonrasiOrani(decimal adisyonToplami, decimal indirimTutari)
    {
        decimal araToplam = adisyonToplami + indirimTutari;
        return araToplam == 0 ? 0 : adisyonToplami / araToplam;
    }

    private async Task<(GunRaporu Rapor, List<AdisyonOzeti> Adisyonlar)> AralikRaporuVeAdisyonlarAsync(
        DateTime gunBasi, DateTime gunSonu, int enCokSatanSayisi)
    {
        var gununHepsi = await GununAdisyonOzetleriAsync(gunBasi, gunSonu);
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= gunBasi && iz.Saat < gunSonu);
        var odenenler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();

        // En çok satan 5 ürün: sadece ödenen adisyonların satırları
        var satilanSatirlar = await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => satir.Siparis.Durum == AdisyonDurumu.Odendi
                         && satir.Siparis.Tarih >= gunBasi && satir.Siparis.Tarih < gunSonu)
            .Select(satir => new { satir.Urun.Ad, satir.Adet, BirimFiyat = satir.IkramMi ? 0 : satir.BirimFiyat })
            .ToListAsync();
        var enCokSatanlar = satilanSatirlar
            .GroupBy(satir => satir.Ad)
            .Select(grup => new KirilimSatiri(grup.Key, grup.Sum(satir => satir.Adet), grup.Sum(satir => satir.Adet * satir.BirimFiyat)))
            .OrderByDescending(kirilim => kirilim.Adet).ThenByDescending(kirilim => kirilim.Tutar)
            .Take(enCokSatanSayisi)
            .ToList();

        // Saatlere göre sipariş sayısı (iptaller hariç): en yoğun saatleri görmek için
        var saatlik = gununHepsi
            .Where(adisyon => adisyon.Durum != AdisyonDurumu.Iptal)
            .GroupBy(adisyon => adisyon.Tarih.Hour)
            .Select(grup => new SaatSatiri(grup.Key, grup.Count()))
            .OrderBy(saat => saat.Saat)
            .ToList();

        var rapor = new GunRaporu(gunBasi, OzetHesapla(gununHepsi, silinenler, await IkramlariGetirAsync(gunBasi, gunSonu)),
            OdemeKirilimi(odenenler), KaynakKirilimi(odenenler), enCokSatanlar, saatlik);
        return (rapor, gununHepsi);
    }

    // ===================== AY RAPORU (her ay sonu özet) =====================

    public async Task<AyRaporu> AyRaporuAsync(int yil, int ay)
    {
        DateTime ayBasi = new(yil, ay, 1);
        DateTime aySonu = ayBasi.AddMonths(1);

        var ayinHepsi = await GununAdisyonOzetleriAsync(ayBasi, aySonu);
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= ayBasi && iz.Saat < aySonu);
        var odenenler = ayinHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();

        // Gün gün: GroupBy ile her günün adisyonları bir gruba toplanır
        var gunler = ayinHepsi
            .GroupBy(adisyon => adisyon.Tarih.Date)
            .OrderBy(grup => grup.Key)
            .Select(grup => new GunSatiri(
                grup.Key,
                grup.Count(adisyon => adisyon.Durum == AdisyonDurumu.Odendi),
                grup.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).Sum(adisyon => adisyon.Toplam),
                grup.Count(adisyon => adisyon.Durum == AdisyonDurumu.Iptal)))
            .ToList();

        return new AyRaporu(yil, ay, gunler, OzetHesapla(ayinHepsi, silinenler, await IkramlariGetirAsync(ayBasi, aySonu)),
            OdemeKirilimi(odenenler), KaynakKirilimi(odenenler));
    }

    // ===================== YARDIMCILAR =====================

    // Ödeme kırılımı: platform siparişinin ödeme türü yok, parası uygulamada ödendi -> platformun adı (Getir...)
    private static List<KirilimSatiri> OdemeKirilimi(List<AdisyonOzeti> odenenler) => odenenler
        .GroupBy(adisyon => adisyon.OdemeTuru ?? (Kaynaklar.PlatformMu(adisyon.Kaynak) ? adisyon.Kaynak : "Online"))
        .Select(grup => new KirilimSatiri(grup.Key, grup.Count(), grup.Sum(adisyon => adisyon.Toplam)))
        .OrderByDescending(kirilim => kirilim.Tutar)
        .ToList();

    // Kaynağa göre: sıra satış ekranındaki gibi (Masa, Paket, platformlar). Platformlarda komisyon da var.
    private static List<KirilimSatiri> KaynakKirilimi(List<AdisyonOzeti> odenenler) => odenenler
        .GroupBy(adisyon => adisyon.Kaynak)
        .Select(grup => new KirilimSatiri(grup.Key, grup.Count(), grup.Sum(adisyon => adisyon.Toplam),
            grup.Sum(adisyon => adisyon.Komisyon)))
        .OrderBy(kirilim => Array.IndexOf(Kaynaklar.Hepsi, kirilim.Ad))
        .ToList();

    // Günün bütün adisyonlarının özet satırları (ürün satırları olmadan): rapor hesapları bunun üstünde yapılır
    private async Task<List<AdisyonOzeti>> GununAdisyonOzetleriAsync(DateTime gunBasi, DateTime gunSonu) =>
        await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Tarih >= gunBasi && siparis.Tarih < gunSonu)
            .Select(siparis => new AdisyonOzeti(
                siparis.Durum,
                siparis.Kaynak,
                siparis.OdemeTuru != null ? siparis.OdemeTuru.Ad : null,
                siparis.Tarih,
                siparis.Toplam,
                siparis.KomisyonOrani,
                siparis.IndirimTutari,
                siparis.KisiSayisi))
            .ToListAsync();

    private static GunOzeti OzetHesapla(List<AdisyonOzeti> gununHepsi, List<SilinenBilgisi> silinenler,
        List<IkramSatiri> ikramlar)
    {
        var odenenler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();
        var iptaller = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Iptal).ToList();
        var iadeler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Iade).ToList();
        return new GunOzeti(
            OdenenSayisi: odenenler.Count,
            Ciro: odenenler.Sum(adisyon => adisyon.Toplam),
            IptalSayisi: iptaller.Count,
            IptalTutari: iptaller.Sum(adisyon => adisyon.Toplam),
            AcikSayisi: gununHepsi.Count(adisyon => adisyon.Durum == AdisyonDurumu.Acik),
            SilinenUrunAdedi: silinenler.Sum(iz => iz.Adet),
            SilinenTutar: silinenler.Sum(iz => iz.Tutar),
            Komisyon: odenenler.Sum(adisyon => adisyon.Komisyon),
            IndirimToplami: odenenler.Sum(adisyon => adisyon.IndirimTutari),
            IadeSayisi: iadeler.Count,
            IadeTutari: iadeler.Sum(adisyon => adisyon.Toplam),
            IkramAdedi: ikramlar.Sum(ikram => ikram.Adet),
            IkramTutari: ikramlar.Sum(ikram => ikram.Adet * ikram.BirimFiyat));
    }

    // Ödenen adisyonlardaki ikram satırları (ürün ücretsiz verildi; normal fiyatı raporda görünür)
    private async Task<List<IkramSatiri>> IkramlariGetirAsync(DateTime bas, DateTime son) =>
        await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => satir.IkramMi && satir.Siparis.Durum == AdisyonDurumu.Odendi
                         && satir.Siparis.Tarih >= bas && satir.Siparis.Tarih < son)
            .Select(satir => new IkramSatiri(satir.Adet, satir.BirimFiyat))
            .ToListAsync();

    private record IkramSatiri(int Adet, decimal BirimFiyat);

    private record AdisyonOzeti(AdisyonDurumu Durum, string Kaynak, string? OdemeTuru, DateTime Tarih,
        decimal Toplam, decimal KomisyonOrani, decimal IndirimTutari, int? KisiSayisi)
    {
        // Siparis.Komisyon ile aynı hesap
        public decimal Komisyon => Math.Round(Toplam * KomisyonOrani / 100, 2);
    }

    // Silinen ürün izleri. Koşul dışarıdan gelir (gün ya da adisyon), sorgunun geri kalanı ortak.
    private async Task<List<SilinenBilgisi>> SilinenleriGetirAsync(
        System.Linq.Expressions.Expression<Func<SilinenSatir, bool>> kosul)
    {
        var hamIzler = await veritabani.SilinenSatirlar.AsNoTracking()
            .Where(kosul)
            .OrderBy(iz => iz.Saat)
            .Select(iz => new
            {
                iz.SiparisId,
                iz.AdisyonBasligi,
                iz.UrunAdi,
                iz.Adet,
                iz.BirimFiyat,
                iz.Saat,
                Kullanici = iz.Kullanici.KullaniciAdi,
                AdisyonDuruyorMu = veritabani.Siparisler.Any(siparis => siparis.Id == iz.SiparisId)
            })
            .ToListAsync();

        return hamIzler
            .Select(iz => new SilinenBilgisi(iz.SiparisId, iz.AdisyonBasligi, iz.UrunAdi, iz.Adet,
                iz.Adet * iz.BirimFiyat, iz.Saat, iz.Kullanici, iz.AdisyonDuruyorMu))
            .ToList();
    }
}

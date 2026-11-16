using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Sadece OKUR, hiçbir şeyi değiştirmez. Adisyon paneli, Bugün ve Gün sonu sayfaları buradan beslenir.
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

        // Rakamlar ve kırılımlar: dönemin tamamı (en çok satan 10 ürün)
        var (rapor, adisyonlar) = await AralikRaporuVeAdisyonlarAsync(bas, son, enCokSatanSayisi: 10);
        var odenenler = adisyonlar.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();

        // Bir önceki aynı uzunluktaki dönem (önceki gün / geçen hafta / geçen ay)
        DateTime oncekiBas = filtre.Donem == Donem.Ay ? bas.AddMonths(-1) : bas - (son - bas);
        decimal oncekiCiro = await OncekiCiroAsync(bas, son, oncekiBas, bas);

        // Günlük çubuklar: dönemin her günü (satış olmayan gün 0)
        var gunler = new List<GunlukSatir>();
        for (DateTime gun = bas; gun < son; gun = gun.AddDays(1))
        {
            var gununkiler = odenenler.Where(adisyon => adisyon.Tarih.Date == gun).ToList();
            gunler.Add(new GunlukSatir(gun, gununkiler.Count, gununkiler.Sum(adisyon => adisyon.Toplam),
                gununkiler.Sum(adisyon => adisyon.Komisyon)));
        }

        // Kategoriye göre satış (ikram satırları 0 ₺ sayılır). Adisyon iskontosu satırlara tutarları oranında
        // dağıtılır: böylece kategorilerin toplamı ciroya eşit olur.
        var kategoriSatirlari = await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => satir.Siparis.Durum == AdisyonDurumu.Odendi && satir.Siparis.Tarih >= bas && satir.Siparis.Tarih < son)
            .Select(satir => new
            {
                satir.Urun.Kategori,
                satir.Adet,
                BirimFiyat = satir.IkramMi ? 0 : satir.BirimFiyat,
                AdisyonToplami = satir.Siparis.Toplam,
                satir.Siparis.IndirimTutari
            })
            .ToListAsync();
        var kategoriKirilimi = kategoriSatirlari
            .GroupBy(satir => satir.Kategori)
            .Select(grup => new KirilimSatiri(grup.Key, grup.Sum(satir => satir.Adet),
                Math.Round(grup.Sum(satir => satir.Adet * satir.BirimFiyat * IskontoSonrasiOrani(satir.AdisyonToplami, satir.IndirimTutari)), 2)))
            .OrderByDescending(kirilim => kirilim.Tutar)
            .ToList();

        var kayitlar = await KayitListesiAsync(bas, son, filtre.Durum, filtre.Kaynak, filtre.OdemeTuruId);
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= bas && iz.Saat < son);
        var odemeTurleri = await veritabani.OdemeTurleri.AsNoTracking().OrderBy(odeme => odeme.Id).ToListAsync();

        return new AdisyonPaneli(filtre, rapor, oncekiCiro,
            odenenler.Where(adisyon => adisyon.KomisyonOrani > 0).Sum(adisyon => adisyon.Toplam),
            gunler, kategoriKirilimi, kayitlar, silinenler, odemeTurleri);
    }

    // Dönemin adisyon listesi; durum / kaynak / ödeme seçildiyse süzülür
    private async Task<List<KayitSatiri>> KayitListesiAsync(DateTime bas, DateTime son,
        AdisyonDurumu? durumSuzgeci, string? kaynakSuzgeci, int? odemeSuzgeci)
    {
        // Adisyon hangi gün açıldıysa o güne sayılır (dükkan 12'de kapanıyor)
        var gununAdisyonlari = veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Tarih >= bas && siparis.Tarih < son);
        var filtre = new { Durum = durumSuzgeci, Kaynak = kaynakSuzgeci, OdemeTuruId = odemeSuzgeci };

        // 1) Filtreyi uygula: her seçim sorguya bir Where daha ekler (henüz veritabanına gidilmedi)
        var suzulmus = gununAdisyonlari;
        if (filtre.Durum is AdisyonDurumu durum)
        {
            suzulmus = suzulmus.Where(siparis => siparis.Durum == durum);
        }
        if (!string.IsNullOrWhiteSpace(filtre.Kaynak))
        {
            suzulmus = suzulmus.Where(siparis => siparis.Kaynak == filtre.Kaynak);
        }
        if (filtre.OdemeTuruId is int odemeTuruId)
        {
            suzulmus = suzulmus.Where(siparis => siparis.OdemeTuruId == odemeTuruId);
        }

        // 2) Sadece gereken sütunları çek (Select = SQL'de SELECT ...)
        var hamKayitlar = await suzulmus
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
            IndirimAciklamasi(adisyon),
            adisyon.IndirimYapanKullanici?.KullaniciAdi,
            adisyon.IadeNedeni,
            adisyon.IadeEdenKullanici?.KullaniciAdi,
            adisyon.IadeSaati);
    }

    private static string? IndirimAciklamasi(Siparis adisyon)
    {
        if (adisyon.IndirimDegeri <= 0)
        {
            return null;
        }
        string deger = adisyon.IndirimYuzdeMi ? $"%{adisyon.IndirimDegeri:0.##}" : $"{adisyon.IndirimDegeri:N2} ₺";
        return adisyon.IndirimNedeni == null ? deger : $"{deger} · {adisyon.IndirimNedeni}";
    }

    // ===================== GÜN RAPORU (Bugün sayfası, ileride Gün sonu) =====================

    public Task<GunRaporu> GunRaporuAsync(DateTime gun) => AralikRaporuAsync(gun.Date, gun.Date.AddDays(1));

    // ===================== PANO (Bugün sayfası: bugün / dün / bu hafta) =====================

    public async Task<PanoRaporu> PanoAsync(PanoAraligi aralik)
    {
        DateTime bugun = DateTime.Today;
        var (baslangic, bitis) = aralik switch
        {
            PanoAraligi.Dun => (bugun.AddDays(-1), bugun),
            PanoAraligi.Hafta => (RaporHesabi.HaftaBasi(bugun), bugun.AddDays(1)),
            _ => (bugun, bugun.AddDays(1))
        };

        var rapor = await AralikRaporuAsync(baslangic, bitis);

        // Karşılaştırma: bir hafta önceki aynı aralık (aynı gün ya da geçen hafta)
        decimal oncekiCiro = await OncekiCiroAsync(baslangic, bitis, baslangic.AddDays(-7), bitis.AddDays(-7));
        var turkce = new System.Globalization.CultureInfo("tr-TR");
        string oncekiAdi = aralik == PanoAraligi.Hafta
            ? "geçen hafta"
            : "geçen " + baslangic.ToString("dddd", turkce).ToLower(turkce);

        // Aralığın adisyonları: ilk açılış saati, platform cirosu ve son siparişler bunlardan
        var adisyonlar = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Tarih >= baslangic && siparis.Tarih < bitis)
            .Select(siparis => new
            {
                siparis.Id,
                siparis.Tarih,
                siparis.Kaynak,
                MasaAdi = siparis.Masa != null ? siparis.Masa.Ad : null,
                siparis.Toplam,
                siparis.Durum,
                siparis.KomisyonOrani
            })
            .ToListAsync();

        decimal platformCirosu = adisyonlar
            .Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi && adisyon.KomisyonOrani > 0)
            .Sum(adisyon => adisyon.Toplam);

        // Son 6 sipariş: ürünleri ayrı sorguyla (sadece bu 6 adisyonun satırları)
        var sonAltisi = adisyonlar.OrderByDescending(adisyon => adisyon.Tarih).Take(6).ToList();
        var sonIdler = sonAltisi.Select(adisyon => adisyon.Id).ToList();
        var sonSatirlar = await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => sonIdler.Contains(satir.SiparisId))
            .OrderBy(satir => satir.Id)
            .Select(satir => new { satir.SiparisId, satir.Urun.Ad, satir.Adet })
            .ToListAsync();
        var sonSiparisler = sonAltisi
            .Select(adisyon => new SonSiparis(
                adisyon.Id,
                adisyon.Tarih,
                adisyon.Kaynak,
                adisyon.MasaAdi ?? AdisyonServisi.PaketBasligi(adisyon.Kaynak, adisyon.Tarih),
                string.Join(", ", sonSatirlar
                    .Where(satir => satir.SiparisId == adisyon.Id)
                    .Select(satir => satir.Adet > 1 ? $"{satir.Adet}× {satir.Ad}" : satir.Ad)),
                adisyon.Toplam,
                adisyon.Durum))
            .ToList();

        return new PanoRaporu(aralik, baslangic, bitis, rapor, oncekiCiro, oncekiAdi,
            adisyonlar.Count == 0 ? null : adisyonlar.Min(adisyon => adisyon.Tarih),
            platformCirosu, sonSiparisler);
    }

    // Önceki dönemin cirosu. Şu anki dönem henüz bitmediyse (bugün, bu hafta, bu ay) önceki dönem de
    // aynı noktaya kadar sayılır: öğlen bugünü geçen haftanın bütün günüyle kıyaslamak yanıltır.
    private async Task<decimal> OncekiCiroAsync(DateTime bas, DateTime son, DateTime oncekiBas, DateTime oncekiSon)
    {
        DateTime simdi = DateTime.Now;
        if (simdi < son)
        {
            DateTime ayniNokta = oncekiBas + (simdi - bas);
            if (ayniNokta < oncekiSon)
            {
                oncekiSon = ayniNokta;
            }
        }

        var toplamlar = await veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi && siparis.Tarih >= oncekiBas && siparis.Tarih < oncekiSon)
            .Select(siparis => siparis.Toplam)
            .ToListAsync();
        return toplamlar.Sum();
    }

    // İskonto sonrası kalan pay: 100 ₺'lik adisyona 10 ₺ iskonto -> her satır 0,9 ile çarpılır
    private static decimal IskontoSonrasiOrani(decimal adisyonToplami, decimal indirimTutari)
    {
        decimal araToplam = adisyonToplami + indirimTutari;
        return araToplam == 0 ? 0 : adisyonToplami / araToplam;
    }

    // Bir zaman aralığının raporu (bir gün ya da bir hafta)
    private async Task<GunRaporu> AralikRaporuAsync(DateTime gunBasi, DateTime gunSonu, int enCokSatanSayisi = 5) =>
        (await AralikRaporuVeAdisyonlarAsync(gunBasi, gunSonu, enCokSatanSayisi)).Rapor;

    // Rapor + aralığın adisyon özetleri: Adisyon paneli aynı listeyi günlük grafikte de kullanır (ikinci sorgu olmasın)
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

    // Ödeme kırılımı: platform siparişinin ödeme türü yok, parası uygulamada ödendi -> "Online"
    private static List<KirilimSatiri> OdemeKirilimi(List<AdisyonOzeti> odenenler) => odenenler
        .GroupBy(adisyon => adisyon.OdemeTuru ?? "Online")
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
                siparis.IndirimTutari))
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
        decimal Toplam, decimal KomisyonOrani, decimal IndirimTutari)
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

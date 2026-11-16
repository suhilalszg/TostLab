using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Sadece OKUR, hiçbir şeyi değiştirmez. Kayıtlar, Bugün ve Gün sonu sayfaları buradan beslenir.
// Not: SQLite ondalık (decimal) sayıları SQL içinde toplayamaz. Bir günün kaydı az olduğu için
// satırları çekip toplamı C# tarafında (LINQ to Objects) alıyoruz.
public class RaporServisi
{
    private readonly TostLabDbContext veritabani;

    public RaporServisi(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    public async Task<KayitlarSayfasi> KayitlarAsync(KayitFiltresi filtre)
    {
        DateTime gunBasi = filtre.SecilenGun;
        DateTime gunSonu = gunBasi.AddDays(1);

        // Adisyon hangi gün açıldıysa o güne sayılır (dükkan 12'de kapanıyor)
        var gununAdisyonlari = veritabani.Siparisler.AsNoTracking()
            .Where(siparis => siparis.Tarih >= gunBasi && siparis.Tarih < gunSonu);

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

        var kayitlar = hamKayitlar
            .Select(kayit => new KayitSatiri(
                kayit.Id,
                kayit.MasaAdi ?? AdisyonServisi.PaketBasligi(kayit.Kaynak, kayit.Tarih),
                kayit.Kaynak, kayit.Durum, kayit.Tarih, kayit.KapanisSaati,
                kayit.OdemeTuru, kayit.UrunAdedi, kayit.Toplam))
            .ToList();

        // 3) Özet: filtreden bağımsız, günün tamamı
        var gununHepsi = await GununAdisyonOzetleriAsync(gunBasi, gunSonu);
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= gunBasi && iz.Saat < gunSonu);
        var ozet = OzetHesapla(gununHepsi, silinenler);

        var odemeTurleri = await veritabani.OdemeTurleri.AsNoTracking().OrderBy(odeme => odeme.Id).ToListAsync();

        return new KayitlarSayfasi(filtre, ozet, kayitlar, silinenler, odemeTurleri);
    }

    public async Task<AdisyonDetayi?> AdisyonDetayiAsync(int adisyonId)
    {
        var adisyon = await veritabani.Siparisler.AsNoTracking()
            .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
            .Include(siparis => siparis.Masa)
            .Include(siparis => siparis.OdemeTuru)
            .Include(siparis => siparis.IptalEdenKullanici)
            .FirstOrDefaultAsync(siparis => siparis.Id == adisyonId);
        if (adisyon == null)
        {
            return null;
        }

        var satirlar = adisyon.Satirlar
            .OrderBy(satir => satir.Id)
            .Select(satir => new SatirBilgisi(satir.Id, satir.UrunId, satir.Urun.Ad, satir.Adet, satir.BirimFiyat, satir.Tutar))
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
            await SilinenleriGetirAsync(iz => iz.SiparisId == adisyonId));
    }

    // ===================== GÜN RAPORU (Bugün sayfası, ileride Gün sonu) =====================

    public async Task<GunRaporu> GunRaporuAsync(DateTime gun)
    {
        DateTime gunBasi = gun.Date;
        DateTime gunSonu = gunBasi.AddDays(1);

        var gununHepsi = await GununAdisyonOzetleriAsync(gunBasi, gunSonu);
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= gunBasi && iz.Saat < gunSonu);
        var odenenler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();

        // En çok satan 5 ürün: sadece ödenen adisyonların satırları
        var satilanSatirlar = await veritabani.SiparisSatirlari.AsNoTracking()
            .Where(satir => satir.Siparis.Durum == AdisyonDurumu.Odendi
                         && satir.Siparis.Tarih >= gunBasi && satir.Siparis.Tarih < gunSonu)
            .Select(satir => new { satir.Urun.Ad, satir.Adet, satir.BirimFiyat })
            .ToListAsync();
        var enCokSatanlar = satilanSatirlar
            .GroupBy(satir => satir.Ad)
            .Select(grup => new KirilimSatiri(grup.Key, grup.Sum(satir => satir.Adet), grup.Sum(satir => satir.Adet * satir.BirimFiyat)))
            .OrderByDescending(kirilim => kirilim.Adet).ThenByDescending(kirilim => kirilim.Tutar)
            .Take(5)
            .ToList();

        // Saatlere göre sipariş sayısı (iptaller hariç): en yoğun saatleri görmek için
        var saatlik = gununHepsi
            .Where(adisyon => adisyon.Durum != AdisyonDurumu.Iptal)
            .GroupBy(adisyon => adisyon.Tarih.Hour)
            .Select(grup => new SaatSatiri(grup.Key, grup.Count()))
            .OrderBy(saat => saat.Saat)
            .ToList();

        return new GunRaporu(gunBasi, OzetHesapla(gununHepsi, silinenler),
            OdemeKirilimi(odenenler), KaynakKirilimi(odenenler), enCokSatanlar, saatlik);
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

        return new AyRaporu(yil, ay, gunler, OzetHesapla(ayinHepsi, silinenler),
            OdemeKirilimi(odenenler), KaynakKirilimi(odenenler));
    }

    // ===================== YARDIMCILAR =====================

    // Ödeme türüne göre: platform siparişinin ödeme türü yok, parası uygulamada ödendi -> "Online"
    private static List<KirilimSatiri> OdemeKirilimi(List<AdisyonOzeti> odenenler) => odenenler
        .GroupBy(adisyon => adisyon.OdemeTuru ?? "Online")
        .Select(grup => new KirilimSatiri(grup.Key, grup.Count(), grup.Sum(adisyon => adisyon.Toplam)))
        .OrderByDescending(kirilim => kirilim.Tutar)
        .ToList();

    // Kaynağa göre: sıra satış ekranındaki gibi (Masa, Paket, platformlar)
    private static List<KirilimSatiri> KaynakKirilimi(List<AdisyonOzeti> odenenler) => odenenler
        .GroupBy(adisyon => adisyon.Kaynak)
        .Select(grup => new KirilimSatiri(grup.Key, grup.Count(), grup.Sum(adisyon => adisyon.Toplam)))
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
                siparis.Toplam))
            .ToListAsync();

    private static GunOzeti OzetHesapla(List<AdisyonOzeti> gununHepsi, List<SilinenBilgisi> silinenler)
    {
        var odenenler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();
        var iptaller = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Iptal).ToList();
        return new GunOzeti(
            OdenenSayisi: odenenler.Count,
            Ciro: odenenler.Sum(adisyon => adisyon.Toplam),
            IptalSayisi: iptaller.Count,
            IptalTutari: iptaller.Sum(adisyon => adisyon.Toplam),
            AcikSayisi: gununHepsi.Count(adisyon => adisyon.Durum == AdisyonDurumu.Acik),
            SilinenUrunAdedi: silinenler.Sum(iz => iz.Adet),
            SilinenTutar: silinenler.Sum(iz => iz.Tutar));
    }

    private record AdisyonOzeti(AdisyonDurumu Durum, string Kaynak, string? OdemeTuru, DateTime Tarih, decimal Toplam);

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

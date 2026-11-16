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
        var gununHepsi = await gununAdisyonlari
            .Select(siparis => new { siparis.Durum, siparis.Toplam })
            .ToListAsync();
        var silinenler = await SilinenleriGetirAsync(iz => iz.Saat >= gunBasi && iz.Saat < gunSonu);

        var odenenler = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Odendi).ToList();
        var iptaller = gununHepsi.Where(adisyon => adisyon.Durum == AdisyonDurumu.Iptal).ToList();
        var ozet = new GunOzeti(
            OdenenSayisi: odenenler.Count,
            Ciro: odenenler.Sum(adisyon => adisyon.Toplam),
            IptalSayisi: iptaller.Count,
            IptalTutari: iptaller.Sum(adisyon => adisyon.Toplam),
            AcikSayisi: gununHepsi.Count(adisyon => adisyon.Durum == AdisyonDurumu.Acik),
            SilinenUrunAdedi: silinenler.Sum(iz => iz.Adet),
            SilinenTutar: silinenler.Sum(iz => iz.Tutar));

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

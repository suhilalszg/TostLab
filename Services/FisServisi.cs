using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Fiş kayıtları: adisyon ödenince fişi olduğu gibi saklanır, Fişler ekranında günlük / aylık listelenir.
// Fiş silmek satışı silmez: sadece fiş kayıtlarından kalkar (raporlar adisyonlardan hesaplanır).
public class FisServisi
{
    private readonly TostLabDbContext veritabani;

    public FisServisi(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    // Ödeme anında çağrılır (AdisyonServisi). Satırların Urun'u yüklü olmalı.
    public static FisKaydi Olustur(Siparis siparis, string baslik, string? odemeTuruAdi, string? kasiyer, DateTime odemeAni) => new()
    {
        Siparis = siparis,
        Tarih = odemeAni,
        Baslik = baslik,
        Kaynak = siparis.Kaynak,
        OdemeTuru = odemeTuruAdi ?? (Kaynaklar.PlatformMu(siparis.Kaynak) ? $"Online ({siparis.Kaynak})" : "—"),
        KisiSayisi = siparis.KisiSayisi,
        Kasiyer = kasiyer,
        AraToplam = siparis.AraToplam,
        IndirimTutari = siparis.IndirimTutari,
        IndirimAciklamasi = IndirimAciklamasi(siparis),
        Toplam = siparis.Toplam,
        Satirlar = siparis.Satirlar
            .OrderBy(satir => satir.Id)
            .Select(satir => new FisSatiri
            {
                UrunAdi = satir.Urun.Ad,
                Adet = satir.Adet,
                BirimFiyat = satir.BirimFiyat,
                Tutar = satir.Tutar,
                IkramMi = satir.IkramMi
            })
            .ToList()
    };

    // "%10 · tanıdık" ya da "20,00 ₺"
    public static string? IndirimAciklamasi(Siparis adisyon)
    {
        if (adisyon.IndirimDegeri <= 0)
        {
            return null;
        }
        string deger = adisyon.IndirimYuzdeMi ? $"%{adisyon.IndirimDegeri:0.##}" : $"{adisyon.IndirimDegeri:N2} ₺";
        return adisyon.IndirimNedeni == null ? deger : $"{deger} · {adisyon.IndirimNedeni}";
    }

    // Program açılışında: fiş kaydı özelliğinden önce ödenmiş adisyonların fişini bir kez oluşturur.
    // Fiş tablosu doluysa iş bitmiştir (o andan sonra her ödeme kendi fişini yazar): adisyonlar her açılışta taranmaz.
    // Silinen fişin satırı durduğu için (SilinmeSaati dolu) silinen fiş geri gelmez, tablo da boşalmaz.
    public static void EksikFisleriOlustur(TostLabDbContext veritabani)
    {
        if (veritabani.FisKayitlari.Any())
        {
            return;
        }
        var eksikler = veritabani.Siparisler
            .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
            .Include(siparis => siparis.Masa)
            .Include(siparis => siparis.OdemeTuru)
            .Include(siparis => siparis.AcanKullanici)
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi || siparis.Durum == AdisyonDurumu.Iade)
            .AsSplitQuery()
            .ToList();
        if (eksikler.Count == 0)
        {
            return;
        }

        foreach (var siparis in eksikler)
        {
            veritabani.FisKayitlari.Add(Olustur(siparis,
                siparis.Masa?.Ad ?? AdisyonServisi.PaketBasligi(siparis.Kaynak, siparis.Tarih),
                siparis.OdemeTuru?.Ad, siparis.AcanKullanici?.KullaniciAdi,
                siparis.KapanisSaati ?? siparis.Tarih));
        }
        veritabani.SaveChanges();
    }

    // Fişler ekranı: seçilen gün ya da ayın silinmemiş fişleri (yeniden eskiye), sayfa sayfa.
    // Önce dönemin bütün fişlerinin özeti (hafif: sayı ve gün sayıları için), sonra sadece
    // gösterilen sayfanın fişleri satırlarıyla yüklenir.
    public async Task<FisListesi> ListeAsync(FisFiltresi filtre)
    {
        var donemdekiler = await veritabani.FisKayitlari.AsNoTracking()
            .Where(fis => fis.SilinmeSaati == null && fis.Tarih >= filtre.Baslangic && fis.Tarih < filtre.Bitis)
            .OrderByDescending(fis => fis.Tarih).ThenByDescending(fis => fis.Id)
            // Ödendikten sonra iade edilen adisyonun fişine "İADE" damgası basılır
            .Select(fis => new FisOzeti(fis.Id, fis.Tarih, fis.OdemeTuru, fis.Siparis.Durum == AdisyonDurumu.Iade))
            .ToListAsync();

        // Ödeme türü süzgeci (dönemde bulunanlar arasından)
        var odemeTurleri = donemdekiler.Select(fis => fis.OdemeTuru).Distinct().OrderBy(ad => ad).ToList();
        var hepsi = string.IsNullOrEmpty(filtre.Odeme)
            ? donemdekiler
            : donemdekiler.Where(fis => fis.OdemeTuru == filtre.Odeme).ToList();

        int sayfaSayisi = Math.Max(1, (hepsi.Count + FisFiltresi.SayfaBoyu - 1) / FisFiltresi.SayfaBoyu);
        int sayfa = Math.Clamp(filtre.Sayfa, 1, sayfaSayisi);
        var sayfadakiOzetler = hepsi.Skip((sayfa - 1) * FisFiltresi.SayfaBoyu).Take(FisFiltresi.SayfaBoyu).ToList();
        var sayfadakiIdler = sayfadakiOzetler.Select(fis => fis.Id).ToList();
        var kayitlar = await veritabani.FisKayitlari.AsNoTracking()
            .Where(fis => sayfadakiIdler.Contains(fis.Id))
            .Include(fis => fis.Satirlar)
            .ToDictionaryAsync(fis => fis.Id);
        var sayfadakiler = sayfadakiOzetler
            .Select(ozet => new FisGorunumu(kayitlar[ozet.Id], ozet.IadeMi))
            .ToList();

        return new FisListesi(filtre, sayfadakiler, hepsi, odemeTurleri, sayfa, sayfaSayisi);
    }

    // Fişi kayıtlardan kaldırır. Satış ve raporlar etkilenmez.
    public async Task<bool> SilAsync(int fisId, int kullaniciId)
    {
        var fis = await veritabani.FisKayitlari.FirstOrDefaultAsync(kayit => kayit.Id == fisId && kayit.SilinmeSaati == null);
        if (fis == null)
        {
            return false;
        }
        fis.SilinmeSaati = DateTime.Now;
        fis.SilenKullaniciId = kullaniciId;
        await veritabani.SaveChangesAsync();
        return true;
    }
}

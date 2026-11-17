namespace TostLab.Models;

// Ödenen adisyonun fişi: ödeme anında olduğu gibi saklanır (ürün adı / fiyatı sonradan değişse de fiş değişmez).
// Fişler ekranında günlük / aylık listelenir. Silinirse sadece fiş kayıtlarından kalkar (SilinmeSaati dolar);
// satış ve raporlar etkilenmez. Silinen fiş geri gelmesin diye satır tutulur.
public class FisKaydi
{
    public int Id { get; set; }                     // fiş numarası

    public int SiparisId { get; set; }              // bir adisyonun bir fişi olur
    public Siparis Siparis { get; set; } = null!;

    public DateTime Tarih { get; set; }             // ödeme anı
    public string Baslik { get; set; } = "";        // "İç 2" ya da "Paket 14:05"
    public string Kaynak { get; set; } = "";
    public string OdemeTuru { get; set; } = "";     // "Nakit", "Kart"... platformda "Online (Getir)"
    public int? KisiSayisi { get; set; }
    public string? Kasiyer { get; set; }            // adisyonu açan kullanıcı

    public decimal AraToplam { get; set; }
    public decimal IndirimTutari { get; set; }
    public string? IndirimAciklamasi { get; set; }  // "%10 · tanıdık"
    public decimal Toplam { get; set; }

    public List<FisSatiri> Satirlar { get; set; } = new();

    public DateTime? SilinmeSaati { get; set; }
    public int? SilenKullaniciId { get; set; }
    public Kullanici? SilenKullanici { get; set; }
}

public class FisSatiri
{
    public int Id { get; set; }
    public int FisKaydiId { get; set; }
    public FisKaydi FisKaydi { get; set; } = null!;

    public string UrunAdi { get; set; } = "";
    public int Adet { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }              // ikramda 0
    public bool IkramMi { get; set; }
}

public enum FisDonemi { Gun, Ay }

// Fişler ekranının süzgeci: /Fisler?donem=Ay&tarih=2026-11-01&odeme=Nakit&sayfa=2
public class FisFiltresi
{
    public const int SayfaBoyu = 48;

    public FisDonemi Donem { get; set; } = FisDonemi.Gun;
    public DateTime? Tarih { get; set; }
    public string? Odeme { get; set; }
    public int Sayfa { get; set; } = 1;

    public DateTime SecilenTarih => Tarih is DateTime tarih && tarih.Year is >= 2000 and <= 2100 ? tarih.Date : DateTime.Today;
    public DateTime Baslangic => Donem == FisDonemi.Ay ? new DateTime(SecilenTarih.Year, SecilenTarih.Month, 1) : SecilenTarih;
    public DateTime Bitis => Donem == FisDonemi.Ay ? Baslangic.AddMonths(1) : Baslangic.AddDays(1);   // dahil değil
    public DateTime OncekiTarih => Donem == FisDonemi.Ay ? Baslangic.AddMonths(-1) : Baslangic.AddDays(-1);
    public DateTime SonrakiTarih => Bitis;
}

// Dönemin her fişinin özeti (sayı ve gün sayıları bundan); satırları sadece gösterilen sayfa için yüklenir
public record FisOzeti(int Id, DateTime Tarih, string OdemeTuru, bool IadeMi);

public record FisGorunumu(FisKaydi Fis, bool IadeMi);

public record FisListesi(FisFiltresi Filtre, List<FisGorunumu> Sayfadakiler, List<FisOzeti> Hepsi,
    List<string> OdemeTurleri, int Sayfa, int SayfaSayisi)
{
    public int GunFisSayisi(DateTime gun) => Hepsi.Count(fis => fis.Tarih.Date == gun);
}

// Ekrana basılan fiş: Fişler ekranı ve adisyon detayı aynı görünümü (Views/Shared/_Fis) kullanır
public record FisCiktiSatiri(int Adet, string Ad, decimal Tutar, bool IkramMi);

public record FisCiktisi(
    string Numara,                  // "Fiş #12 · İç 2" ya da "Adisyon #40 · İç 2"
    string Zaman,
    List<FisCiktiSatiri> Satirlar,
    decimal IndirimTutari,
    string? IndirimAciklamasi,
    decimal Toplam,
    List<string> AltSatirlar,       // "Ödeme: Nakit", "Kişi: 2"...
    AdisyonDurumu? Damga)           // damga basılacak durum (yoksa damgasız)
{
    public decimal AraToplam => Toplam + IndirimTutari;

    public static FisCiktisi Kayittan(FisKaydi fis, bool iadeMi)
    {
        var alt = new List<string> { $"Ödeme: {fis.OdemeTuru}" };
        if (fis.KisiSayisi is int kisi) alt.Add($"Kişi: {kisi}");
        if (fis.Kasiyer != null) alt.Add($"Açan: {fis.Kasiyer}");
        return new FisCiktisi($"Fiş #{fis.Id} · {fis.Baslik}", fis.Tarih.ToString("d MMMM yyyy HH:mm"),
            fis.Satirlar.OrderBy(satir => satir.Id).Select(satir => new FisCiktiSatiri(satir.Adet, satir.UrunAdi, satir.Tutar, satir.IkramMi)).ToList(),
            fis.IndirimTutari, fis.IndirimAciklamasi, fis.Toplam, alt, iadeMi ? AdisyonDurumu.Iade : null);
    }

    public static FisCiktisi Detaydan(AdisyonDetayi adisyon)
    {
        var alt = new List<string> { $"Kaynak: {adisyon.Kaynak}" };
        if (adisyon.OdemeTuru != null) alt.Add($"Ödeme: {adisyon.OdemeTuru}");
        else if (Kaynaklar.PlatformMu(adisyon.Kaynak) && adisyon.Durum == AdisyonDurumu.Odendi) alt.Add($"Ödeme: Online ({adisyon.Kaynak})");
        if (adisyon.IndirimYapan != null) alt.Add($"İndirimi yapan: {adisyon.IndirimYapan}");
        if (adisyon.KisiSayisi is int kisi) alt.Add($"Kişi: {kisi}");
        string zaman = adisyon.Acilis.ToString("d MMMM yyyy HH:mm") + (adisyon.Kapanis is DateTime kapanis ? $" – {kapanis:HH:mm}" : "");
        return new FisCiktisi($"Adisyon #{adisyon.Id} · {adisyon.Baslik}", zaman,
            adisyon.Satirlar.Select(satir => new FisCiktiSatiri(satir.Adet, satir.Ad, satir.Tutar, satir.IkramMi)).ToList(),
            adisyon.IndirimTutari, adisyon.IndirimAciklamasi, adisyon.Toplam, alt,
            adisyon.Durum == AdisyonDurumu.Acik ? null : adisyon.Durum);
    }
}

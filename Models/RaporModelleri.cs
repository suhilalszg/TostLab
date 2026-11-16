namespace TostLab.Models;

// Kayıtlar (ve ileride Bugün / Gün sonu) sayfalarının veri şekilleri.
// Sadece okumak için: veritabanı sınıflarını (Siparis) sayfaya doğrudan vermiyoruz,
// sayfanın ihtiyacı olan alanları taşıyan küçük kayıtlar (DTO) veriyoruz.

// Adres çubuğundan gelir: /Kayitlar?gun=2026-11-16&durum=Iptal&kaynak=Getir&odemeTuruId=1
public class KayitFiltresi
{
    public DateTime? Gun { get; set; }
    public AdisyonDurumu? Durum { get; set; }
    public string? Kaynak { get; set; }
    public int? OdemeTuruId { get; set; }

    // Gün seçilmediyse bugün
    public DateTime SecilenGun => (Gun ?? DateTime.Today).Date;
}

// Listedeki bir satır
public record KayitSatiri(
    int Id,
    string Baslik,              // "İç 2" ya da "Getir 14:05"
    string Kaynak,
    AdisyonDurumu Durum,
    DateTime Acilis,
    DateTime? Kapanis,
    string? OdemeTuru,          // platform siparişinde ve açık adisyonda boş
    int UrunAdedi,
    decimal Toplam);

// Günün özeti: filtreden bağımsız, o günün tamamı
public record GunOzeti(
    int OdenenSayisi,
    decimal Ciro,               // sadece ödenenler
    int IptalSayisi,
    decimal IptalTutari,
    int AcikSayisi,
    int SilinenUrunAdedi,
    decimal SilinenTutar);

public record SilinenBilgisi(
    int SiparisId,
    string AdisyonBasligi,
    string UrunAdi,
    int Adet,
    decimal Tutar,
    DateTime Saat,
    string Kullanici,
    bool AdisyonDuruyorMu);     // son ürün silinince adisyon da silinir: o zaman detaya gidilemez

public record KayitlarSayfasi(
    KayitFiltresi Filtre,
    GunOzeti Ozet,
    List<KayitSatiri> Kayitlar,
    List<SilinenBilgisi> Silinenler,
    List<OdemeTuru> OdemeTurleri);

// Bir adisyonun tam hali (fiş)
public record AdisyonDetayi(
    int Id,
    string Baslik,
    string Kaynak,
    AdisyonDurumu Durum,
    DateTime Acilis,
    DateTime? Kapanis,
    string? OdemeTuru,
    int? KisiSayisi,
    decimal Toplam,
    List<SatirBilgisi> Satirlar,
    string? IptalNedeni,
    string? IptalEden,
    List<SilinenBilgisi> Silinenler);

public static class DurumYazisi
{
    public static string Yaz(AdisyonDurumu durum) => durum switch
    {
        AdisyonDurumu.Acik => "Açık",
        AdisyonDurumu.Odendi => "Ödendi",
        AdisyonDurumu.Iptal => "İptal",
        _ => durum.ToString()
    };
}

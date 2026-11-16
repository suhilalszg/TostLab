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
    decimal SilinenTutar,
    decimal Komisyon)           // platformların kestiği toplam
{
    // Dükkana kalan: ciro - platform komisyonu
    public decimal NetKazanc => Ciro - Komisyon;
}

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

// Bir günün raporu: Bugün sayfası (ve 10. adımda Gün sonu) bunu gösterir
public record GunRaporu(
    DateTime Gun,
    GunOzeti Ozet,
    List<KirilimSatiri> OdemeKirilimi,     // Nakit, Kart, Multinet..., Online
    List<KirilimSatiri> KaynakKirilimi,    // Masa, Paket, Getir...
    List<KirilimSatiri> EnCokSatanlar,     // ilk 5 ürün
    List<SaatSatiri> Saatlik)
{
    // Ortalama adisyon tutarı (hiç ödeme yoksa 0)
    public decimal OrtalamaAdisyon => Ozet.OdenenSayisi == 0 ? 0 : Ozet.Ciro / Ozet.OdenenSayisi;
}

// Kırılımın bir satırı: "Nakit · 12 adisyon · 3.450 ₺" ya da "Sade · 18 adet · 1.800 ₺"
// Komisyon sadece kaynak kırılımında dolu (platform satırları)
public record KirilimSatiri(string Ad, int Adet, decimal Tutar, decimal Komisyon = 0);

public record SaatSatiri(int Saat, int Adet);

// Aylık özet: her gün bir satır + ayın toplamı
public record AyRaporu(
    int Yil,
    int Ay,
    List<GunSatiri> Gunler,                // sadece kaydı olan günler
    GunOzeti Toplam,
    List<KirilimSatiri> OdemeKirilimi,
    List<KirilimSatiri> KaynakKirilimi)
{
    public decimal GunlukOrtalamaCiro => Gunler.Count == 0 ? 0 : Toplam.Ciro / Gunler.Count;
}

public record GunSatiri(DateTime Gun, int OdenenSayisi, decimal Ciro, int IptalSayisi);

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

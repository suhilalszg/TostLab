namespace TostLab.Models;

// Adisyon paneli, Bugün ve Gün sonu sayfalarının veri şekilleri.
// Sadece okumak için: veritabanı sınıflarını (Siparis) sayfaya doğrudan vermiyoruz,
// sayfanın ihtiyacı olan alanları taşıyan küçük kayıtlar (DTO) veriyoruz.

// Adisyon panelinin dönemi: bir gün, o günün haftası (pazartesiden) ya da o günün ayı
public enum Donem { Gun, Hafta, Ay }

// Adres çubuğundan gelir: /Adisyon?donem=Hafta&tarih=2026-11-16&durum=Iptal&kaynak=Getir&odemeTuruId=1
// Durum / kaynak / ödeme sadece adisyon listesini süzer; grafikler ve rakamlar dönemin tamamıdır.
public class PanelFiltresi
{
    public Donem Donem { get; set; } = Donem.Gun;
    public DateTime? Tarih { get; set; }
    public AdisyonDurumu? Durum { get; set; }
    public string? Kaynak { get; set; }
    public int? OdemeTuruId { get; set; }

    public DateTime SecilenTarih => (Tarih ?? DateTime.Today).Date;

    public DateTime Baslangic => Donem switch
    {
        Donem.Hafta => SecilenTarih.AddDays(-(((int)SecilenTarih.DayOfWeek + 6) % 7)),   // pazartesi
        Donem.Ay => new DateTime(SecilenTarih.Year, SecilenTarih.Month, 1),
        _ => SecilenTarih
    };

    // Dahil değil
    public DateTime Bitis => Donem switch
    {
        Donem.Hafta => Baslangic.AddDays(7),
        Donem.Ay => Baslangic.AddMonths(1),
        _ => Baslangic.AddDays(1)
    };

    // Önceki / sonraki dönem düğmeleri için
    public DateTime OncekiTarih => Donem switch { Donem.Hafta => Baslangic.AddDays(-7), Donem.Ay => Baslangic.AddMonths(-1), _ => Baslangic.AddDays(-1) };
    public DateTime SonrakiTarih => Bitis;
}

// Adisyon paneli: dönemin bütün rakamları, grafikleri ve adisyon listesi
public record AdisyonPaneli(
    PanelFiltresi Filtre,
    GunRaporu Rapor,                       // özet, ödeme / kaynak kırılımı, en çok satanlar, saatlik
    decimal OncekiCiro,                    // bir önceki dönemin cirosu (0 = satış yok)
    decimal PlatformCirosu,
    List<GunlukSatir> Gunler,              // dönemin her günü (hafta / ay grafiği)
    List<KirilimSatiri> KategoriKirilimi,
    List<KayitSatiri> Kayitlar,            // süzgeçten geçen adisyonlar
    List<SilinenBilgisi> Silinenler,
    List<OdemeTuru> OdemeTurleri)
{
    public decimal? DegisimYuzdesi => OncekiCiro == 0 ? null : (Rapor.Ozet.Ciro - OncekiCiro) * 100 / OncekiCiro;
    public decimal KomisyonYuzdesi => PlatformCirosu == 0 ? 0 : Rapor.Ozet.Komisyon * 100 / PlatformCirosu;
}

// Günlük grafik çubuğu
public record GunlukSatir(DateTime Gun, int Adet, decimal Ciro, decimal Komisyon);

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
    decimal Komisyon,           // platformların kestiği toplam
    decimal IndirimToplami,     // ödenen adisyonlarda yapılan indirim (ciro zaten indirimli)
    int IadeSayisi,             // ödendikten sonra parası geri verilen (ciroya girmez)
    decimal IadeTutari,
    int IkramAdedi,             // ücretsiz verilen ürün adedi (ödenen adisyonlarda)
    decimal IkramTutari)        // o ürünlerin normal fiyatı
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
    List<SilinenBilgisi> Silinenler,
    decimal IndirimTutari,
    string? IndirimAciklamasi,  // "%10 · tanıdık" ya da "20 ₺"
    string? IndirimYapan,
    string? IadeNedeni,
    string? IadeEden,
    DateTime? IadeSaati)
{
    public decimal AraToplam => Toplam + IndirimTutari;
}

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

// Bugün sayfasının (pano) aralığı: adres çubuğunda ?aralik=dun / ?aralik=hafta
public enum PanoAraligi { Bugun, Dun, Hafta }

// Pano: seçilen aralığın raporu + karşılaştırma + son siparişler
public record PanoRaporu(
    PanoAraligi Aralik,
    DateTime Baslangic,
    DateTime Bitis,                        // dahil değil
    GunRaporu Rapor,
    decimal OncekiCiro,                    // karşılaştırılan aralığın cirosu (0 = o aralıkta satış yok)
    string OncekiAdi,                      // "geçen cumartesi" / "geçen hafta"
    DateTime? IlkSiparis,                  // aralığın ilk adisyonunun açılış saati
    decimal PlatformCirosu,                // komisyon kesilen siparişlerin cirosu
    List<SonSiparis> SonSiparisler)
{
    // Ciro değişimi yüzdesi: önceki aralıkta satış yoksa hesaplanamaz
    public decimal? DegisimYuzdesi => OncekiCiro == 0 ? null : (Rapor.Ozet.Ciro - OncekiCiro) * 100 / OncekiCiro;

    // Komisyon, platform cirosunun yüzde kaçı
    public decimal KomisyonYuzdesi => PlatformCirosu == 0 ? 0 : Rapor.Ozet.Komisyon * 100 / PlatformCirosu;
}

// Son siparişler tablosunun bir satırı. Urunler: "2× Ayran, Kaşarlı Tost"
public record SonSiparis(int Id, DateTime Saat, string Kaynak, string Baslik, string Urunler, decimal Toplam, AdisyonDurumu Durum);

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
        AdisyonDurumu.Iade => "İade",
        _ => durum.ToString()
    };
}

namespace TostLab.Models;

// Adisyon: masaya ilk ürün girilince (ya da paket siparişi başlayınca) oluşur,
// ürünler eklendikçe büyür, ödeme alınınca kapanır.
public class Siparis
{
    public int Id { get; set; }

    // Adisyonun açıldığı an
    public DateTime Tarih { get; set; }

    public AdisyonDurumu Durum { get; set; } = AdisyonDurumu.Acik;

    // Nereden geldi: "Masa", "Paket", "Yemeksepeti", "Getir", "Trendyol", "Migros"
    public string Kaynak { get; set; } = "";

    // Sadece masa siparişinde dolu
    public int? MasaId { get; set; }
    public Masa? Masa { get; set; }

    // Ödeme alınınca dolar
    public int? OdemeTuruId { get; set; }
    public OdemeTuru? OdemeTuru { get; set; }
    public DateTime? KapanisSaati { get; set; }

    // Masadaki kişi sayısı (isteğe bağlı)
    public int? KisiSayisi { get; set; }

    // Ödenecek tutar = satırların toplamı - indirim; her değişiklikte yeniden hesaplanır
    public decimal Toplam { get; set; }

    // İndirim: kasada girilen değer. YuzdeMi ise 10 = %10, değilse 10 = 10 ₺.
    // Yüzde indirim ürün eklenince yeniden hesaplanır; tutar indirim satır toplamını geçemez.
    public bool IndirimYuzdeMi { get; set; }
    public decimal IndirimDegeri { get; set; }
    // Değerden hesaplanan ₺ karşılığı (raporlar bunu toplar)
    public decimal IndirimTutari { get; set; }
    public string? IndirimNedeni { get; set; }
    public int? IndirimYapanKullaniciId { get; set; }
    public Kullanici? IndirimYapanKullanici { get; set; }

    // İndirimden önceki tutar
    public decimal AraToplam => Toplam + IndirimTutari;

    // Platformun kestiği yüzde, ödeme anında yazılır (masa ve telefon paketinde 0)
    public decimal KomisyonOrani { get; set; }

    public decimal Komisyon => Math.Round(Toplam * KomisyonOrani / 100, 2);

    public List<SiparisSatiri> Satirlar { get; set; } = new();

    // İptal bilgileri: sadece Durum = Iptal ise dolu
    public string? IptalNedeni { get; set; }
    public DateTime? IptalSaati { get; set; }
    public int? IptalEdenKullaniciId { get; set; }
    public Kullanici? IptalEdenKullanici { get; set; }

    // Hâlâ masada mı (ödenmedi, iptal edilmedi)?
    public bool AcikMi => Durum == AdisyonDurumu.Acik;
}

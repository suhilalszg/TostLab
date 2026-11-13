namespace TostLab.Models;

public class Siparis
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }

    // Nereden geldi: "Masa", "Paket", "Yemeksepeti", "Getir", "Trendyol", "Migros"
    public string Kaynak { get; set; } = "";

    // Sadece masa siparişinde dolu
    public int? MasaId { get; set; }
    public Masa? Masa { get; set; }

    public int OdemeTuruId { get; set; }
    public OdemeTuru OdemeTuru { get; set; } = null!;

    public decimal Toplam { get; set; }

    public List<SiparisSatiri> Satirlar { get; set; } = new();

    // İptal bilgileri: sipariş iptal edilmediyse boş kalır
    public bool IptalEdildiMi { get; set; }
    public string? IptalNedeni { get; set; }
    public DateTime? IptalSaati { get; set; }
    public int? IptalEdenKullaniciId { get; set; }
    public Kullanici? IptalEdenKullanici { get; set; }
}

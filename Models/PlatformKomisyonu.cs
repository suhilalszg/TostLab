namespace TostLab.Models;

// Yemeksepeti, Getir, Trendyol, Migros'un siparişten kestiği yüzde.
// Yönetici Ayarlar'dan değiştirir. Oran değişince eski siparişler bozulmasın diye
// ödeme anındaki oran ayrıca siparişe yazılır (Siparis.KomisyonOrani), tıpkı BirimFiyat gibi.
public class PlatformKomisyonu
{
    public int Id { get; set; }

    // Kaynaklar.Platformlar'dan biri: "Getir"
    public string Kaynak { get; set; } = "";

    // Yüzde: 18 = %18
    public decimal Oran { get; set; }
}

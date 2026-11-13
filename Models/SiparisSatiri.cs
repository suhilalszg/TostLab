namespace TostLab.Models;

public class SiparisSatiri
{
    public int Id { get; set; }

    public int SiparisId { get; set; }
    public Siparis Siparis { get; set; } = null!;

    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;

    public int Adet { get; set; } = 1;

    // Sipariş anındaki fiyat: ürünün fiyatı sonradan değişse de bu değişmez
    public decimal BirimFiyat { get; set; }

    public decimal Tutar => Adet * BirimFiyat;
}

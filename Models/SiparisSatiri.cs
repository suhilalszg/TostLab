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

    // İkram: ürün adisyonda görünür ama ücret alınmaz. Kimin ikram ettiği saklanır.
    public bool IkramMi { get; set; }
    public int? IkramEdenKullaniciId { get; set; }
    public Kullanici? IkramEdenKullanici { get; set; }

    public decimal Tutar => IkramMi ? 0 : Adet * BirimFiyat;

    // İkram edilen ürünün normal fiyatı (raporda "ikram toplamı" için)
    public decimal IkramTutari => IkramMi ? Adet * BirimFiyat : 0;
}

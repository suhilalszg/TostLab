namespace TostLab.Models;

public class Urun
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public decimal Fiyat { get; set; }
    public string Kategori { get; set; } = "";
    public string? Icindekiler { get; set; }
    public bool SatistaMi { get; set; } = true;

    // Bir adet ürünün hammadde maliyeti (₺). 0 = girilmedi: kâr hesabına katılmaz.
    public decimal Maliyet { get; set; }

    // Menü'den yüklenen fotoğrafın dosya adı (veri/urun-foto klasöründe).
    // Yoksa satış ekranında ürünün baş harfi görünür.
    public string? FotografAdi { get; set; }
}

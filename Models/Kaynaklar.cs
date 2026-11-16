namespace TostLab.Models;

// Siparişin nereden geldiği. Veritabanına bu yazılar kaydedilir.
public static class Kaynaklar
{
    public const string Masa = "Masa";
    public const string Paket = "Paket";

    public static readonly string[] Hepsi =
    {
        Masa, Paket, "Yemeksepeti", "Getir", "Trendyol", "Migros"
    };
}

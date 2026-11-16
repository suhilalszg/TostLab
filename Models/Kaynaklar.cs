namespace TostLab.Models;

// Siparişin nereden geldiği. Veritabanına bu yazılar kaydedilir.
public static class Kaynaklar
{
    public const string Masa = "Masa";
    public const string Paket = "Paket";

    // Parası uygulamada (online) ödenen siparişler: kasada ödeme türü sorulmaz
    public static readonly string[] Platformlar = { "Yemeksepeti", "Getir", "Trendyol", "Migros" };

    public static readonly string[] Hepsi = [Masa, Paket, .. Platformlar];

    public static bool PlatformMu(string kaynak) => Platformlar.Contains(kaynak);
}

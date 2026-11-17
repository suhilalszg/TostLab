using Microsoft.EntityFrameworkCore;
using TostLab.Models;

namespace TostLab.Data;

// Dükkânın menüsü (17 Kasım 2026 menü görseli). Ürün tablosu boşsa program ilk açılışta bunları ekler.
// Daha önce kurulmuş veritabanında aynı menüyü YeniMenu migration'ı uygular.
public static class BaslangicMenusu
{
    // Menü görselindeki ürünler ve fiyatları (migration da bunu kullanır)
    public static readonly (string Kategori, string Ad, decimal Fiyat, string Icindekiler)[] YeniMenu =
    {
        ("Burger Menü", "Original Smash Menü", 350, "Smash burger + patates kızartması + kutu içecek (330 ml) veya ayran"),
        ("Burger Menü", "Classic Zip Menü", 350, "Classic burger + patates kızartması + kutu içecek (330 ml) veya ayran"),
        ("Burger Menü", "Big Zip Menü", 450, "Çift köfteli burger + patates kızartması + kutu içecek (330 ml) veya ayran"),
        ("Burger Menü", "Crispy Chicken Menü", 280, "Çıtır tavuk burger + patates kızartması + kutu içecek (330 ml) veya ayran"),

        ("Tost", "Ayvalık Tost", 260, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Kumru Tost", 250, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Patso", 180, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Sanayi Tost", 270, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Karışık Bazlama", 250, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Karışık Tost", 250, "Yanında patates kızartması ve turşu ikram"),
        ("Tost", "Kavurma Kaşarlı Tost", 375, "Yanında patates kızartması ve turşu ikram"),
    };

    // İçecekler: fiyatları menüde yok (menülerde dahil), yönetici Menü sayfasından girer
    private static readonly string[] icecekler =
    {
        "Kola", "Kola Zero", "Fanta", "Sprite", "Gazoz", "Ayran", "Su", "Soda", "Limonata", "Ice Tea", "Enerji İçeceği"
    };

    public static void BossaYukle(TostLabDbContext veritabani)
    {
        if (veritabani.Urunler.Any())
        {
            return;   // menü daha önce yüklenmiş: dokunma
        }

        foreach (var (kategori, ad, fiyat, icindekiler) in YeniMenu)
        {
            veritabani.Urunler.Add(new Urun { Kategori = kategori, Ad = ad, Fiyat = fiyat, Icindekiler = icindekiler });
        }
        foreach (var icecek in icecekler)
        {
            veritabani.Urunler.Add(new Urun { Kategori = "İçecek", Ad = icecek, Fiyat = 0 });
        }
        veritabani.SaveChanges();
    }
}

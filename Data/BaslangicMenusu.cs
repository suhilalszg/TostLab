using Microsoft.EntityFrameworkCore;
using TostLab.Models;

namespace TostLab.Data;

// Abinin gönderdiği menü (13 Kasım 2026). Fiyatlar henüz gelmedi: hepsi 0 ₺ başlar,
// yönetici Menü sayfasından girer. Ürün tablosu boşsa program ilk açılışta bunları ekler.
public static class BaslangicMenusu
{
    private static readonly (string Kategori, string Ad, string? Icindekiler)[] urunler =
    {
        ("Klasikler", "Sade", "Kaşar peyniri"),
        ("Klasikler", "Tek", "Sucuk"),
        ("Klasikler", "Temel", "Sucuk, kaşar peyniri"),
        ("Klasikler", "Kızıl", "Sucuk, kaşar peyniri, salça"),
        ("Klasikler", "Yeni Nesil Yengen", "Sucuk, kaşar peyniri, Rus salatası"),
        ("Klasikler", "Kızıl Yengen", "Sucuk, kaşar peyniri, Rus salatası, salça"),
        ("Klasikler", "Kampüs", "Sosis, kaşar peyniri, Rus salatası, salça"),
        ("Klasikler", "Maksimum", "Kaşar peyniri, Rus salatası, turşu, ketçap"),

        ("Atom (Yumurtalı) Seri", "Günaydın", "Yumurta, kaşar peyniri"),
        ("Atom (Yumurtalı) Seri", "Erkenci", "Yumurta, sucuk, kaşar peyniri"),
        ("Atom (Yumurtalı) Seri", "Tempo", "Yumurta, sosis, kaşar peyniri"),
        ("Atom (Yumurtalı) Seri", "Mesai", "Yumurta, dana kavurma, kaşar peyniri"),
        ("Atom (Yumurtalı) Seri", "Pazar", "Yumurta, pastırma, kaşar peyniri"),

        ("Ağır Sanayi Serisi", "Usta", "Sucuk, kaşar, yumurta, taze domates, yeşil biber, salça"),
        ("Ağır Sanayi Serisi", "Tamirhane", "Kavurma, sucuk, kaşar, yumurta, taze domates, biber, salça"),
        ("Ağır Sanayi Serisi", "Ağır Sanayi", "Pastırma, sucuk, kaşar, yumurta, domates, biber, salça"),

        ("Şarküteri Serisi", "Demirbaş", "Dana kavurma, kaşar peyniri"),
        ("Şarküteri Serisi", "Modern", "Dana füme, kaşar veya çedar peyniri"),
        ("Şarküteri Serisi", "Lin Pesto", "Dana füme, kaşar veya çedar peyniri"),
        ("Şarküteri Serisi", "Ferah", "Dana füme, kaşar veya çedar peyniri, taze domates, yeşil biber"),
        ("Şarküteri Serisi", "Ziyafet", "Pastırma, kaşar peyniri, taze domates, biber"),
        ("Şarküteri Serisi", "Anadolu", "Pastırma, dana kavurma, kaşar peyniri"),
        ("Şarküteri Serisi", "Harman", "Çedar, beyaz peynir, kaşar peyniri, ekstra peynir"),
        ("Şarküteri Serisi", "El Pesto", "Pesto, çedar, beyaz peynir, kaşar peyniri, ekstra peynir"),
        ("Şarküteri Serisi", "İmza", "Dana kavurma, pastırma, dana füme, sucuk, kaşar peyniri, çedar peyniri"),

        ("Bazlama Serisi", "Kök", "Bazlama, sucuk, kaşar peyniri"),
        ("Bazlama Serisi", "Köz", "Bazlama, dana kavurma, kaşar peyniri"),
        ("Bazlama Serisi", "Bostan", "Bazlama, kaşar peyniri, domates, biber"),
        ("Bazlama Serisi", "Harman", "Bazlama, sucuk, kaşar peyniri, yumurta, domates, biber, salça"),

        ("Hafif Seri", "Ada", "Beyaz peynir, domates"),
        ("Hafif Seri", "Yeşil", "Beyaz peynir, domates, pesto"),
        ("Hafif Seri", "Ege", "Kaşar peyniri, domates, kekik"),
        ("Hafif Seri", "Kumsal", "Kaşar peyniri, domates, biber"),

        ("Spesiyal Serisi", "Ayvalık", "Sosis, sucuk, salam, kaşar peyniri, turşu, Rus salatası, ketçap, mayonez, domates"),
        ("Spesiyal Serisi", "Susurluk", "Mihaliç peyniri, özel salça"),
        ("Spesiyal Serisi", "Napoli", "Kaşar peyniri, sucuk, zeytin, taze biber, pizza sosu"),
        ("Spesiyal Serisi", "Çeşme (Kumru)", "Kumru ekmeğinde sosis, erimiş kaşar peyniri, kornişon turşu"),
        ("Spesiyal Serisi", "Ankara", "Kumru ekmeğinde sosis, kaşar peyniri, kornişon turşu, Rus salatası"),

        ("Patso Serisi", "Sokak", "Patates kızartması, kornişon turşu, ketçap, mayonez"),
        ("Patso Serisi", "Cadde", "Patates kızartması, sosis, kornişon turşu, ketçap, mayonez"),
        ("Patso Serisi", "Meydan", "Patates kızartması, sosis, kaşar peyniri, kornişon turşu, ketçap, mayonez"),

        ("Pattis Serisi", "Patost", "Sucuk, kaşar, patates"),
        ("Pattis Serisi", "Bolpatost", "Sucuk, kaşar, patates, salça, turşu"),
        ("Pattis Serisi", "Fümetes", "Füme, kaşar, patates"),

        ("Tatlı Serisi", "Ödül", "Çikolata kreması, muz, fındık"),

        ("Smash Burger", "Original Smash", "100 g dana smash köfte, cheddar, kornişon turşu, özel burger sosu"),
        ("Smash Burger", "Double Trouble", "2 x 100 g dana smash köfte, çift cheddar, karamelize soğan, kornişon turşu, özel sos"),
        ("Smash Burger", "Hot Zip Smash", "100 g dana smash köfte, cheddar, jalapeño biberi, kornişon turşu, acılı mayonez"),
        ("Smash Burger", "Smash Supreme", "2 x 100 g dana smash köfte, çift cheddar, karamelize soğan, marul, özel burger sosu"),

        ("Classic Burger", "Classic Zip", "100 g dana burger köftesi, cheddar, marul, domates, kornişon turşu, özel sos"),
        ("Classic Burger", "BBQ King", "100 g dana burger köftesi, cheddar, çıtır soğan, karamelize soğan, BBQ sos"),
        ("Classic Burger", "Mushroom Melt", "100 g dana burger köftesi, mantar sote, kaşar peyniri, karamelize soğan, özel sos"),
        ("Classic Burger", "Pastırmalı Zip", "100 g dana burger köftesi, pastırma, cheddar, karamelize soğan, kornişon turşu, özel burger sosu"),
        ("Classic Burger", "Big Zip", "2 x 100 g dana burger köftesi, çift cheddar, marul, domates, kornişon turşu, özel sos"),

        ("Atıştırmalık", "Klasik Patates", "150 g"),
        ("Atıştırmalık", "Cheddarlı Patates", "150 g, cheddar sos"),
        ("Atıştırmalık", "Soğan Halkası", "6 adet"),
        ("Atıştırmalık", "Çıtır Tavuk", "6 adet"),
        ("Atıştırmalık", "Mozzarella Stick", "6 adet"),
        ("Atıştırmalık", "Mix Atıştırmalık", "Soğan halkası, mozzarella stick, çıtır tavuk, patates"),

        ("Sos", "Özel Burger Sos", null),
        ("Sos", "BBQ Sos", null),
        ("Sos", "Acı Mayonez", null),
        ("Sos", "Ranch Sos", null),
        ("Sos", "Cheddar Sos", null),

        ("İçecek", "Kola", null),
        ("İçecek", "Kola Zero", null),
        ("İçecek", "Fanta", null),
        ("İçecek", "Sprite", null),
        ("İçecek", "Gazoz", null),
        ("İçecek", "Ayran", null),
        ("İçecek", "Su", null),
        ("İçecek", "Soda", null),
        ("İçecek", "Limonata", null),
        ("İçecek", "Ice Tea", null),
        ("İçecek", "Enerji İçeceği", null),
    };

    public static void BossaYukle(TostLabDbContext veritabani)
    {
        if (veritabani.Urunler.Any())
        {
            return;   // menü daha önce yüklenmiş: dokunma
        }

        foreach (var (kategori, ad, icindekiler) in urunler)
        {
            veritabani.Urunler.Add(new Urun
            {
                Kategori = kategori,
                Ad = ad,
                Icindekiler = icindekiler,
                Fiyat = 0
            });
        }
        veritabani.SaveChanges();
    }
}

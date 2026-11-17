using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TostLab.Data;

#nullable disable

namespace TostLab.Migrations
{
    // Dükkânın yeni menüsü (17 Kasım 2026): 4 burger menüsü + 7 tost, fiyatlarıyla.
    // Sadece veri değişir, tablo yapısı aynı (o yüzden model anlık görüntüsü değişmedi).
    // Eski ürünler SİLİNMEZ: geçmiş adisyonlar onlara bağlı. "Satışta değil" yapılır,
    // Menü sayfasından tek dokunuşla geri açılabilir. İçecekler olduğu gibi kalır.
    // Boş (yeni kurulan) veritabanında hiçbir şey yapmaz: orada BaslangicMenusu yeni menüyü yükler.
    [DbContext(typeof(TostLabDbContext))]
    [Migration("20261117090000_YeniMenu")]
    public partial class YeniMenu : Migration
    {
        private static readonly (string Kategori, string Ad, decimal Fiyat, string Icindekiler)[] yeniUrunler =
            BaslangicMenusu.YeniMenu;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Urunler SET SatistaMi = 0 WHERE Kategori <> 'İçecek';");

            foreach (var (kategori, ad, fiyat, icindekiler) in yeniUrunler)
            {
                // Aynı adda ürün varsa (elle eklenmiş olabilir) yenisini eklemek yerine onu güncelle
                migrationBuilder.Sql($"""
                    UPDATE Urunler SET Kategori = {Metin(kategori)}, Fiyat = {Para(fiyat)}, Icindekiler = {Metin(icindekiler)}, SatistaMi = 1
                    WHERE Ad = {Metin(ad)};
                    """);
                migrationBuilder.Sql($"""
                    INSERT INTO Urunler (Ad, Fiyat, Kategori, Icindekiler, SatistaMi)
                    SELECT {Metin(ad)}, {Para(fiyat)}, {Metin(kategori)}, {Metin(icindekiler)}, 1
                    WHERE EXISTS (SELECT 1 FROM Urunler) AND NOT EXISTS (SELECT 1 FROM Urunler WHERE Ad = {Metin(ad)});
                    """);
            }
        }

        // Geri alınırsa: yeni ürünleri satıştan kaldır, eskileri geri aç
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (_, ad, _, _) in yeniUrunler)
            {
                migrationBuilder.Sql($"UPDATE Urunler SET SatistaMi = 0 WHERE Ad = {Metin(ad)};");
            }
            migrationBuilder.Sql($"""
                UPDATE Urunler SET SatistaMi = 1
                WHERE Ad NOT IN ({string.Join(", ", yeniUrunler.Select(urun => Metin(urun.Ad)))});
                """);
        }

        // SQL içinde metin: tek tırnaklar ikilenir
        private static string Metin(string deger) => "'" + deger.Replace("'", "''") + "'";

        // EF Core SQLite'ta ondalık sayılar metin olarak durur: '350.0'
        private static string Para(decimal deger) =>
            "'" + deger.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + "'";
    }
}

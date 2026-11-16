using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class HesapIstendiKaldirildi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Hesap istendi" (1) durumu kaldırıldı: o durumdaki adisyonlar açık (0) olsun
            migrationBuilder.Sql("UPDATE Siparisler SET Durum = 0 WHERE Durum = 1;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

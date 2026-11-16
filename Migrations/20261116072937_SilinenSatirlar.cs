using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class SilinenSatirlar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SilinenSatirlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiparisId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdisyonBasligi = table.Column<string>(type: "TEXT", nullable: false),
                    UrunId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrunAdi = table.Column<string>(type: "TEXT", nullable: false),
                    Adet = table.Column<int>(type: "INTEGER", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "TEXT", nullable: false),
                    Saat = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SilinenSatirlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SilinenSatirlar_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_Tarih",
                table: "Siparisler",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_SilinenSatirlar_KullaniciId",
                table: "SilinenSatirlar",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_SilinenSatirlar_Saat",
                table: "SilinenSatirlar",
                column: "Saat");

            migrationBuilder.CreateIndex(
                name: "IX_SilinenSatirlar_SiparisId",
                table: "SilinenSatirlar",
                column: "SiparisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SilinenSatirlar");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_Tarih",
                table: "Siparisler");
        }
    }
}

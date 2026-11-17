using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class KasaVeKarlilik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KdvOrani",
                table: "Urunler",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "Maliyet",
                table: "Urunler",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AylikHedefler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Yil = table.Column<int>(type: "INTEGER", nullable: false),
                    Ay = table.Column<int>(type: "INTEGER", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AylikHedefler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KasaHareketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false),
                    Tur = table.Column<string>(type: "TEXT", nullable: false),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KasaHareketleri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KasaSayimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Gun = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcilisNakdi = table.Column<decimal>(type: "TEXT", nullable: false),
                    SayilanNakit = table.Column<decimal>(type: "TEXT", nullable: true),
                    Saat = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaSayimlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KasaSayimlari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OdemeTurleri",
                columns: new[] { "Id", "Ad", "AktifMi" },
                values: new object[] { 8, "Açık hesap", true });

            migrationBuilder.CreateIndex(
                name: "IX_AylikHedefler_Yil_Ay",
                table: "AylikHedefler",
                columns: new[] { "Yil", "Ay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_KullaniciId",
                table: "KasaHareketleri",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_Tarih",
                table: "KasaHareketleri",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_KasaSayimlari_Gun",
                table: "KasaSayimlari",
                column: "Gun",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KasaSayimlari_KullaniciId",
                table: "KasaSayimlari",
                column: "KullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AylikHedefler");

            migrationBuilder.DropTable(
                name: "KasaHareketleri");

            migrationBuilder.DropTable(
                name: "KasaSayimlari");

            migrationBuilder.DeleteData(
                table: "OdemeTurleri",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "Maliyet",
                table: "Urunler");
        }
    }
}

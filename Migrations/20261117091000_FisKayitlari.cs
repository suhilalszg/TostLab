using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class FisKayitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "Urunler");

            migrationBuilder.CreateTable(
                name: "FisKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiparisId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Baslik = table.Column<string>(type: "TEXT", nullable: false),
                    Kaynak = table.Column<string>(type: "TEXT", nullable: false),
                    OdemeTuru = table.Column<string>(type: "TEXT", nullable: false),
                    KisiSayisi = table.Column<int>(type: "INTEGER", nullable: true),
                    Kasiyer = table.Column<string>(type: "TEXT", nullable: true),
                    AraToplam = table.Column<decimal>(type: "TEXT", nullable: false),
                    IndirimTutari = table.Column<decimal>(type: "TEXT", nullable: false),
                    IndirimAciklamasi = table.Column<string>(type: "TEXT", nullable: true),
                    Toplam = table.Column<decimal>(type: "TEXT", nullable: false),
                    SilinmeSaati = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SilenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FisKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FisKayitlari_Kullanicilar_SilenKullaniciId",
                        column: x => x.SilenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FisKayitlari_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FisSatirlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FisKaydiId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrunAdi = table.Column<string>(type: "TEXT", nullable: false),
                    Adet = table.Column<int>(type: "INTEGER", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "TEXT", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false),
                    IkramMi = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FisSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FisSatirlari_FisKayitlari_FisKaydiId",
                        column: x => x.FisKaydiId,
                        principalTable: "FisKayitlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FisKayitlari_SilenKullaniciId",
                table: "FisKayitlari",
                column: "SilenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_FisKayitlari_SiparisId",
                table: "FisKayitlari",
                column: "SiparisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FisKayitlari_Tarih",
                table: "FisKayitlari",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_FisSatirlari_FisKaydiId",
                table: "FisSatirlari",
                column: "FisKaydiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FisSatirlari");

            migrationBuilder.DropTable(
                name: "FisKayitlari");

            migrationBuilder.AddColumn<int>(
                name: "KdvOrani",
                table: "Urunler",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);
        }
    }
}

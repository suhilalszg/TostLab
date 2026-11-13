using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class IlkKurulum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciAdi = table.Column<string>(type: "TEXT", nullable: false),
                    SifreHash = table.Column<string>(type: "TEXT", nullable: false),
                    Rol = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Masalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    AktifMi = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Masalar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdemeTurleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    AktifMi = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdemeTurleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Urunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", nullable: false),
                    Fiyat = table.Column<decimal>(type: "TEXT", nullable: false),
                    Kategori = table.Column<string>(type: "TEXT", nullable: false),
                    Icindekiler = table.Column<string>(type: "TEXT", nullable: true),
                    SatistaMi = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urunler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Siparisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kaynak = table.Column<string>(type: "TEXT", nullable: false),
                    MasaId = table.Column<int>(type: "INTEGER", nullable: true),
                    OdemeTuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    Toplam = table.Column<decimal>(type: "TEXT", nullable: false),
                    IptalEdildiMi = table.Column<bool>(type: "INTEGER", nullable: false),
                    IptalNedeni = table.Column<string>(type: "TEXT", nullable: true),
                    IptalSaati = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IptalEdenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Siparisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Siparisler_Kullanicilar_IptalEdenKullaniciId",
                        column: x => x.IptalEdenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_Masalar_MasaId",
                        column: x => x.MasaId,
                        principalTable: "Masalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_OdemeTurleri_OdemeTuruId",
                        column: x => x.OdemeTuruId,
                        principalTable: "OdemeTurleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiparisSatirlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiparisId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Adet = table.Column<int>(type: "INTEGER", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiparisSatirlari_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiparisSatirlari_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Masalar",
                columns: new[] { "Id", "Ad", "AktifMi" },
                values: new object[,]
                {
                    { 1, "İç 1", true },
                    { 2, "İç 2", true },
                    { 3, "İç 3", true },
                    { 4, "İç 4", true },
                    { 5, "Dış 1", true }
                });

            migrationBuilder.InsertData(
                table: "OdemeTurleri",
                columns: new[] { "Id", "Ad", "AktifMi" },
                values: new object[,]
                {
                    { 1, "Nakit", true },
                    { 2, "Kart", true },
                    { 3, "Multinet", true },
                    { 4, "Pluxee (Sodexo)", true },
                    { 5, "Edenred (Ticket)", true },
                    { 6, "Setcard", true },
                    { 7, "Metropol", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_IptalEdenKullaniciId",
                table: "Siparisler",
                column: "IptalEdenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_MasaId",
                table: "Siparisler",
                column: "MasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_OdemeTuruId",
                table: "Siparisler",
                column: "OdemeTuruId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisSatirlari_SiparisId",
                table: "SiparisSatirlari",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisSatirlari_UrunId",
                table: "SiparisSatirlari",
                column: "UrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiparisSatirlari");

            migrationBuilder.DropTable(
                name: "Siparisler");

            migrationBuilder.DropTable(
                name: "Urunler");

            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Masalar");

            migrationBuilder.DropTable(
                name: "OdemeTurleri");
        }
    }
}

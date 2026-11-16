using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class AdisyonDurumlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IptalEdildiMi",
                table: "Siparisler",
                newName: "Durum");

            migrationBuilder.AlterColumn<int>(
                name: "OdemeTuruId",
                table: "Siparisler",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "KapanisSaati",
                table: "Siparisler",
                type: "TEXT",
                nullable: true);

            // Eski ekranda girilen siparişler kayıt anında ödenmişti:
            // iptal edilenler Iptal (3), diğerleri Odendi (2) olsun, yoksa "açık masa" gibi görünürler
            migrationBuilder.Sql("UPDATE Siparisler SET Durum = CASE WHEN Durum = 1 THEN 3 ELSE 2 END, KapanisSaati = Tarih;");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_Durum_MasaId",
                table: "Siparisler",
                columns: new[] { "Durum", "MasaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Siparisler_Durum_MasaId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "KapanisSaati",
                table: "Siparisler");

            migrationBuilder.RenameColumn(
                name: "Durum",
                table: "Siparisler",
                newName: "IptalEdildiMi");

            migrationBuilder.AlterColumn<int>(
                name: "OdemeTuruId",
                table: "Siparisler",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}

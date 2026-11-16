using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class Indirim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IndirimDegeri",
                table: "Siparisler",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IndirimNedeni",
                table: "Siparisler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IndirimTutari",
                table: "Siparisler",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "IndirimYapanKullaniciId",
                table: "Siparisler",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IndirimYuzdeMi",
                table: "Siparisler",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_IndirimYapanKullaniciId",
                table: "Siparisler",
                column: "IndirimYapanKullaniciId");

            migrationBuilder.AddForeignKey(
                name: "FK_Siparisler_Kullanicilar_IndirimYapanKullaniciId",
                table: "Siparisler",
                column: "IndirimYapanKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Siparisler_Kullanicilar_IndirimYapanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_IndirimYapanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IndirimDegeri",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IndirimNedeni",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IndirimTutari",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IndirimYapanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IndirimYuzdeMi",
                table: "Siparisler");
        }
    }
}

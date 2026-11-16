using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class MenuluxIslevleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotografAdi",
                table: "Urunler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IkramEdenKullaniciId",
                table: "SiparisSatirlari",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IkramMi",
                table: "SiparisSatirlari",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AcanKullaniciId",
                table: "Siparisler",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IadeEdenKullaniciId",
                table: "Siparisler",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IadeNedeni",
                table: "Siparisler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IadeSaati",
                table: "Siparisler",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiparisSatirlari_IkramEdenKullaniciId",
                table: "SiparisSatirlari",
                column: "IkramEdenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_AcanKullaniciId",
                table: "Siparisler",
                column: "AcanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_IadeEdenKullaniciId",
                table: "Siparisler",
                column: "IadeEdenKullaniciId");

            migrationBuilder.AddForeignKey(
                name: "FK_Siparisler_Kullanicilar_AcanKullaniciId",
                table: "Siparisler",
                column: "AcanKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Siparisler_Kullanicilar_IadeEdenKullaniciId",
                table: "Siparisler",
                column: "IadeEdenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiparisSatirlari_Kullanicilar_IkramEdenKullaniciId",
                table: "SiparisSatirlari",
                column: "IkramEdenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Siparisler_Kullanicilar_AcanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropForeignKey(
                name: "FK_Siparisler_Kullanicilar_IadeEdenKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropForeignKey(
                name: "FK_SiparisSatirlari_Kullanicilar_IkramEdenKullaniciId",
                table: "SiparisSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_SiparisSatirlari_IkramEdenKullaniciId",
                table: "SiparisSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_AcanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_IadeEdenKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "FotografAdi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "IkramEdenKullaniciId",
                table: "SiparisSatirlari");

            migrationBuilder.DropColumn(
                name: "IkramMi",
                table: "SiparisSatirlari");

            migrationBuilder.DropColumn(
                name: "AcanKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IadeEdenKullaniciId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IadeNedeni",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "IadeSaati",
                table: "Siparisler");
        }
    }
}

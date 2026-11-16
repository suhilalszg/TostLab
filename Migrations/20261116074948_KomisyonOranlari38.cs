using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class KomisyonOranlari38 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 1,
                column: "Oran",
                value: 38m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 2,
                column: "Oran",
                value: 38m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 3,
                column: "Oran",
                value: 38m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 4,
                column: "Oran",
                value: 38m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 1,
                column: "Oran",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 2,
                column: "Oran",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 3,
                column: "Oran",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "PlatformKomisyonlari",
                keyColumn: "Id",
                keyValue: 4,
                column: "Oran",
                value: 0m);
        }
    }
}

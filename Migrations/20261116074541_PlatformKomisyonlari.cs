using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class PlatformKomisyonlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KomisyonOrani",
                table: "Siparisler",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PlatformKomisyonlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kaynak = table.Column<string>(type: "TEXT", nullable: false),
                    Oran = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformKomisyonlari", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PlatformKomisyonlari",
                columns: new[] { "Id", "Kaynak", "Oran" },
                values: new object[,]
                {
                    { 1, "Yemeksepeti", 0m },
                    { 2, "Getir", 0m },
                    { 3, "Trendyol", 0m },
                    { 4, "Migros", 0m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformKomisyonlari_Kaynak",
                table: "PlatformKomisyonlari",
                column: "Kaynak",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformKomisyonlari");

            migrationBuilder.DropColumn(
                name: "KomisyonOrani",
                table: "Siparisler");
        }
    }
}

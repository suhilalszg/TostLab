using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TostLab.Migrations
{
    /// <inheritdoc />
    public partial class AylikRaporGonderimleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GonderilenRaporlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Yil = table.Column<int>(type: "INTEGER", nullable: false),
                    Ay = table.Column<int>(type: "INTEGER", nullable: false),
                    GonderimSaati = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ElleMi = table.Column<bool>(type: "INTEGER", nullable: false),
                    Alici = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GonderilenRaporlar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GonderilenRaporlar_Yil_Ay",
                table: "GonderilenRaporlar",
                columns: new[] { "Yil", "Ay" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GonderilenRaporlar");
        }
    }
}

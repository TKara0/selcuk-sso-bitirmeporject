using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selcuk.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class DenetimKayitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DenetimKayitlari",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZamanUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OlayTuru = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IstemciId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAdresi = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    KisaUserAgent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sonuc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ayrinti = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenetimKayitlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DenetimKayitlari_KullaniciId_Id",
                table: "DenetimKayitlari",
                columns: new[] { "KullaniciId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DenetimKayitlari_ZamanUtc",
                table: "DenetimKayitlari",
                column: "ZamanUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DenetimKayitlari");
        }
    }
}

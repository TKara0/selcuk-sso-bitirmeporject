using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selcuk.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class IstemciOturumlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IstemciOturumlari",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OlusturmaZamani = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    KapanmaZamani = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IstemciOturumlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IstemciOturumlari_KullaniciId",
                table: "IstemciOturumlari",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_IstemciOturumlari_Sid_ClientId",
                table: "IstemciOturumlari",
                columns: new[] { "Sid", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IstemciOturumlari");
        }
    }
}

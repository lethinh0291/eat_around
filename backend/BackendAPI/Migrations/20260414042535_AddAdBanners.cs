using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAdBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "StoreRegistrations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "StoreRegistrations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RadiusMeters",
                table: "StoreRegistrations",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdBanners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdBanners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdBanners_IsActive_SortOrder",
                table: "AdBanners",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdBanners");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "StoreRegistrations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "StoreRegistrations");

            migrationBuilder.DropColumn(
                name: "RadiusMeters",
                table: "StoreRegistrations");
        }
    }
}

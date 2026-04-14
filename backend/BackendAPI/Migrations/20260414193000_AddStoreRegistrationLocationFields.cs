using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendAPI.Migrations
{
    public partial class AddStoreRegistrationLocationFields : Migration
    {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
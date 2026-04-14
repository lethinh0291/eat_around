using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class SyncUserAvatarModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH('Users', 'AvatarUrl') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [AvatarUrl] nvarchar(1000) NULL;
END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH('Users', 'AvatarUrl') IS NOT NULL
BEGIN
    ALTER TABLE [Users] DROP COLUMN [AvatarUrl];
END;
""");
        }
    }
}

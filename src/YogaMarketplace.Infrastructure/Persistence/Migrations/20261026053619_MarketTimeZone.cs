using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "PlatformSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Kolkata");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "PlatformSettings");
        }
    }
}

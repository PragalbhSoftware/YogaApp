using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NearestCityMaxKm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NearestCityMaxKm",
                table: "PlatformSettings",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 100m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NearestCityMaxKm",
                table: "PlatformSettings");
        }
    }
}

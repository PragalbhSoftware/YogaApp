using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackCheckoutSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackPriceCap",
                table: "CheckoutIntents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackSessions",
                table: "CheckoutIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackValidityDays",
                table: "CheckoutIntents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackPriceCap",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "PackSessions",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "PackValidityDays",
                table: "CheckoutIntents");
        }
    }
}

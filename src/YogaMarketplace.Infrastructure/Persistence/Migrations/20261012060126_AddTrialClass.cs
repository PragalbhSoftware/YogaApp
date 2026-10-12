using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrialEnabled",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TrialPrice",
                table: "Services",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "CheckoutIntents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId_ProviderId_IsTrial",
                table: "Bookings",
                columns: new[] { "CustomerId", "ProviderId", "IsTrial" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older code has no Refunded status; Completed keeps the order from being captured again.
            migrationBuilder.Sql("UPDATE CheckoutIntents SET Status = 'Completed' WHERE Status = 'Refunded';");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CustomerId_ProviderId_IsTrial",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TrialEnabled",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "TrialPrice",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "Bookings");
        }
    }
}

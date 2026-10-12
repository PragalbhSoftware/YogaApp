using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelFreeWindowHours",
                table: "CheckoutIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                table: "CheckoutIntents",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LateCancelFeeType",
                table: "CheckoutIntents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Percent");

            migrationBuilder.AddColumn<decimal>(
                name: "LateCancelFeeValue",
                table: "CheckoutIntents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Checkouts opened before this column existed were created under the current settings.
            migrationBuilder.Sql(
                """
                UPDATE c
                SET c.CommissionPercent = s.CommissionPercent,
                    c.CancelFreeWindowHours = s.CancelFreeWindowHours,
                    c.LateCancelFeeType = s.LateCancelFeeType,
                    c.LateCancelFeeValue = s.LateCancelFeeValue
                FROM CheckoutIntents c
                CROSS JOIN (SELECT TOP 1 * FROM PlatformSettings) s;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelFreeWindowHours",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "LateCancelFeeType",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "LateCancelFeeValue",
                table: "CheckoutIntents");
        }
    }
}

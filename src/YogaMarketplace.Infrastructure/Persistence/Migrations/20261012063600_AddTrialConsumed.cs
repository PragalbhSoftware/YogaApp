using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialConsumed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrialConsumed",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // EXEC keeps each statement compiling in the idempotent script, where the column doesn't exist yet.
            // Same rule as TrialRules.Consumes: every trial except an instructor decline or an admin cancel.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE Bookings SET TrialConsumed = 1
                WHERE IsTrial = 1
                  AND Status <> ''Declined''
                  AND NOT (Status = ''Cancelled'' AND COALESCE(CancelledBy, '''') = ''Admin'')');
                """);

            // A duplicate trial refunded at capture never counts, even if a booking row was left behind.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE b SET TrialConsumed = 0
                FROM Bookings b
                JOIN Payments p ON p.BookingId = b.Id
                JOIN CheckoutIntents c ON c.GatewayOrderId = p.GatewayOrderId
                WHERE b.TrialConsumed = 1 AND c.Status = ''Refunded''');
                """);

            migrationBuilder.Sql("""
                EXEC(N'IF EXISTS (
                    SELECT 1 FROM Bookings WHERE TrialConsumed = 1
                    GROUP BY CustomerId, ProviderId HAVING COUNT(*) > 1)
                THROW 50001, ''AddTrialConsumed: some customers have more than one active trial with the same instructor. Cancel or refund the extra trial bookings, then run the migration again.'', 1;');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OneConsumedTrial",
                table: "Bookings",
                columns: new[] { "CustomerId", "ProviderId" },
                unique: true,
                filter: "TrialConsumed = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_OneConsumedTrial",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TrialConsumed",
                table: "Bookings");
        }
    }
}

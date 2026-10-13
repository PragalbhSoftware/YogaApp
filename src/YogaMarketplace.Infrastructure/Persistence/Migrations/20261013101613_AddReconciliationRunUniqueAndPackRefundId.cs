using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationRunUniqueAndPackRefundId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayRefundId",
                table: "CreditBatches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            // Days that already have more than one completed run keep the latest; the rest would break the unique index.
            migrationBuilder.Sql(
                """
                UPDATE ReconciliationRuns SET Status = 'Superseded'
                WHERE Status = 'Completed' AND EXISTS (
                    SELECT 1 FROM ReconciliationRuns AS newer
                    WHERE newer.MarketDate = ReconciliationRuns.MarketDate
                      AND newer.Status = 'Completed'
                      AND (newer.StartedAt > ReconciliationRuns.StartedAt
                           OR (newer.StartedAt = ReconciliationRuns.StartedAt AND newer.Id > ReconciliationRuns.Id)));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRuns_MarketDate_Completed",
                table: "ReconciliationRuns",
                column: "MarketDate",
                unique: true,
                filter: "[Status] = 'Completed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReconciliationRuns_MarketDate_Completed",
                table: "ReconciliationRuns");

            migrationBuilder.Sql("UPDATE ReconciliationRuns SET Status = 'Completed' WHERE Status = 'Superseded';");

            migrationBuilder.DropColumn(
                name: "GatewayRefundId",
                table: "CreditBatches");
        }
    }
}

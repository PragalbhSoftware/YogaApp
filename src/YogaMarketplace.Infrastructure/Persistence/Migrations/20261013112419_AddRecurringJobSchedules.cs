using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringJobSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreditExpiryEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CreditExpiryTime",
                table: "PlatformSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "ReconciliationEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WebhookCleanupEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WebhookCleanupTime",
                table: "PlatformSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "RecurringType",
                table: "BackgroundJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            // Runs the jobs queued for themselves before this change become their tracked run, so none is queued twice.
            migrationBuilder.Sql("""
                UPDATE j SET RecurringType = j.Type
                FROM BackgroundJobs j
                WHERE j.Status = 'Pending'
                  AND j.Type IN ('credits.expire', 'payments.reconcile', 'webhooks.clear-payloads')
                  AND j.Id = (SELECT TOP 1 k.Id FROM BackgroundJobs k
                              WHERE k.Type = j.Type AND k.Status = 'Pending' ORDER BY k.RunAt, k.Id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_OneQueuedRunPerRecurringJob",
                table: "BackgroundJobs",
                column: "RecurringType",
                unique: true,
                filter: "[RecurringType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_OneQueuedRunPerRecurringJob",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "CreditExpiryEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "CreditExpiryTime",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ReconciliationEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WebhookCleanupEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WebhookCleanupTime",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RecurringType",
                table: "BackgroundJobs");
        }
    }
}

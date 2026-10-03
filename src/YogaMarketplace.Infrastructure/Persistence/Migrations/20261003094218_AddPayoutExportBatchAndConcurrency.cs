using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutExportBatchAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExportBatchId",
                table: "PayoutsPending",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutsPending_ExportBatchId",
                table: "PayoutsPending",
                column: "ExportBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayoutsPending_ExportBatchId",
                table: "PayoutsPending");

            migrationBuilder.DropColumn(
                name: "ExportBatchId",
                table: "PayoutsPending");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSettingsAndJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_Policies", table: "Policies");
            migrationBuilder.RenameTable(name: "Policies", newName: "PlatformSettings");
            migrationBuilder.AddPrimaryKey(name: "PK_PlatformSettings", table: "PlatformSettings", column: "Id");

            migrationBuilder.RenameColumn(name: "PlatformFeePercent", table: "PlatformSettings", newName: "CommissionPercent");
            migrationBuilder.RenameColumn(name: "LateCancelFeePercent", table: "PlatformSettings", newName: "LateCancelFeeValue");
            migrationBuilder.AlterColumn<decimal>(
                name: "LateCancelFeeValue",
                table: "PlatformSettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvenienceFee",
                table: "PlatformSettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
            migrationBuilder.AddColumn<string>(
                name: "LateCancelFeeType",
                table: "PlatformSettings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Percent");
            migrationBuilder.AddColumn<string>(
                name: "PayoutCycle",
                table: "PlatformSettings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Weekly");
            migrationBuilder.AddColumn<string>(
                name: "BannerTitle",
                table: "PlatformSettings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BannerSubtitle",
                table: "PlatformSettings",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BannerOffer",
                table: "PlatformSettings",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "PlatformSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvenienceFee",
                table: "CheckoutIntents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CancelFreeWindowHours",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                table: "Bookings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "ConvenienceFee",
                table: "Bookings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
            migrationBuilder.AddColumn<string>(
                name: "LateCancelFeeType",
                table: "Bookings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Percent");
            migrationBuilder.AddColumn<decimal>(
                name: "LateCancelFeeValue",
                table: "Bookings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Existing bookings were made under the current policy, so they keep it. There was no convenience fee.
            migrationBuilder.Sql(
                """
                UPDATE b
                SET b.CommissionPercent = s.CommissionPercent,
                    b.CancelFreeWindowHours = s.CancelFreeWindowHours,
                    b.LateCancelFeeType = s.LateCancelFeeType,
                    b.LateCancelFeeValue = s.LateCancelFeeValue
                FROM Bookings b
                CROSS JOIN (SELECT TOP 1 * FROM PlatformSettings) s;
                """);

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettingsAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettingsAudits_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status_RunAt",
                table: "BackgroundJobs",
                columns: new[] { "Status", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SettingsAudits_AdminUserId",
                table: "SettingsAudits",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SettingsAudits_ChangedAt",
                table: "SettingsAudits",
                column: "ChangedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BackgroundJobs");
            migrationBuilder.DropTable(name: "SettingsAudits");

            migrationBuilder.DropColumn(name: "ConvenienceFee", table: "CheckoutIntents");
            migrationBuilder.DropColumn(name: "CancelFreeWindowHours", table: "Bookings");
            migrationBuilder.DropColumn(name: "CommissionPercent", table: "Bookings");
            migrationBuilder.DropColumn(name: "ConvenienceFee", table: "Bookings");
            migrationBuilder.DropColumn(name: "LateCancelFeeType", table: "Bookings");
            migrationBuilder.DropColumn(name: "LateCancelFeeValue", table: "Bookings");

            // The old schema only knew a percent late fee.
            migrationBuilder.Sql(
                "UPDATE PlatformSettings SET LateCancelFeeValue = 0 WHERE LateCancelFeeType = 'Flat' OR LateCancelFeeValue > 100;");

            migrationBuilder.DropColumn(name: "ConvenienceFee", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "LateCancelFeeType", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "PayoutCycle", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "BannerTitle", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "BannerSubtitle", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "BannerOffer", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "Version", table: "PlatformSettings");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "PlatformSettings");

            migrationBuilder.AlterColumn<decimal>(
                name: "LateCancelFeeValue",
                table: "PlatformSettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);
            migrationBuilder.RenameColumn(name: "LateCancelFeeValue", table: "PlatformSettings", newName: "LateCancelFeePercent");
            migrationBuilder.RenameColumn(name: "CommissionPercent", table: "PlatformSettings", newName: "PlatformFeePercent");

            migrationBuilder.DropPrimaryKey(name: "PK_PlatformSettings", table: "PlatformSettings");
            migrationBuilder.RenameTable(name: "PlatformSettings", newName: "Policies");
            migrationBuilder.AddPrimaryKey(name: "PK_Policies", table: "Policies", column: "Id");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailAlertsEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ErrorAlertCheckMinutes",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "ErrorAlertEmail",
                table: "PlatformSettings",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErrorAlertThrottleMinutes",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "ErrorEventRetentionDays",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "ErrorEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LastCorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Count = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAlertedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorEvents_ErrorType",
                table: "ErrorEvents",
                column: "ErrorType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorEvents_LastSeenAt",
                table: "ErrorEvents",
                column: "LastSeenAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorEvents");

            migrationBuilder.DropColumn(
                name: "EmailAlertsEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ErrorAlertCheckMinutes",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ErrorAlertEmail",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ErrorAlertThrottleMinutes",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ErrorEventRetentionDays",
                table: "PlatformSettings");
        }
    }
}

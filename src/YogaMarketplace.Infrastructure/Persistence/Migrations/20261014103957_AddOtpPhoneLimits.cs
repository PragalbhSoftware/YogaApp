using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpPhoneLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OtpLockMinutes",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "OtpMaxSendsPerPhone",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "OtpMaxWrongCodesPerPhone",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateTable(
                name: "OtpPhoneLocks",
                columns: table => new
                {
                    Phone = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpPhoneLocks", x => x.Phone);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpPhoneLocks");

            migrationBuilder.DropColumn(
                name: "OtpLockMinutes",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OtpMaxSendsPerPhone",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OtpMaxWrongCodesPerPhone",
                table: "PlatformSettings");
        }
    }
}

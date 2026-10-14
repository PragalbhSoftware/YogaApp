using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NotificationMaxAttempts",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateTable(
                name: "NotificationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Data = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DltTemplateId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTemplates_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "CategoryId", "Channel", "DltTemplateId", "EventKey", "IsActive", "Subject" },
                values: new object[,]
                {
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000001"), "Hi {{customerName}},\n\nYour session is booked.\n\nInstructor: {{instructorName}}\nSession: {{serviceTitle}}\nWhen: {{sessionDate}} at {{sessionTime}}\nBooking reference: {{bookingRef}}\n\nYou can see the details and cancel from My bookings.", null, "Email", null, "booking.confirmed.customer", true, "Booked: {{serviceTitle}} with {{instructorName}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000002"), "Hi {{instructorName}},\n\n{{customerName}} booked {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.\nBooking reference: {{bookingRef}}\n\nOpen your bookings to accept or decline the request.", null, "Email", null, "booking.confirmed.instructor", true, "New booking: {{serviceTitle}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000003"), "Hi {{customerName}},\n\nYour booking for {{serviceTitle}} with {{instructorName}} on {{sessionDate}} at {{sessionTime}} is cancelled.\nRefund: {{refund}}\nBooking reference: {{bookingRef}}", null, "Email", null, "booking.cancelled.customer", true, "Cancelled: {{serviceTitle}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000004"), "Hi {{instructorName}},\n\nThe booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}} is cancelled.\nBooking reference: {{bookingRef}}\n\nThe slot is open again for other customers.", null, "Email", null, "booking.cancelled.instructor", true, "Booking cancelled: {{serviceTitle}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000005"), "Hi {{customerName}},\n\nWe refunded your booking for {{serviceTitle}} with {{instructorName}} on {{sessionDate}} at {{sessionTime}}.\nRefund: {{refund}}\nBooking reference: {{bookingRef}}", null, "Email", null, "booking.refunded.customer", true, "Refund for {{serviceTitle}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000006"), "Hi {{instructorName}},\n\nThe booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}} was refunded.\nBooking reference: {{bookingRef}}", null, "Email", null, "booking.refunded.instructor", true, "Booking refunded: {{serviceTitle}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000007"), "Hi {{instructorName}},\n\nWe sent your payout of {{amount}}.\nPayout reference: {{payoutRef}}", null, "Email", null, "payout.paid.instructor", true, "Payout sent: {{amount}}" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_CreatedAt",
                table: "NotificationOutbox",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_CategoryId",
                table: "NotificationTemplates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_OneActivePerEventChannelCategory",
                table: "NotificationTemplates",
                columns: new[] { "EventKey", "Channel", "CategoryId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationOutbox");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "NotificationMaxAttempts",
                table: "PlatformSettings");
        }
    }
}

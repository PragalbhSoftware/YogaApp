using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRequestEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000002"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Hi {{instructorName}},\n\nYou accepted the booking by {{customerName}} for {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.\nBooking reference: {{bookingRef}}", "Booking confirmed: {{serviceTitle}} on {{sessionDate}}" });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "CategoryId", "Channel", "DltTemplateId", "EventKey", "IsActive", "Subject" },
                values: new object[,]
                {
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000008"), "Hi {{customerName}},\n\nWe received your booking request. It is waiting for the instructor to accept it.\n\nInstructor: {{instructorName}}\nSession: {{serviceTitle}}\nWhen: {{sessionDate}} at {{sessionTime}}\nBooking reference: {{bookingRef}}\n\nWe'll email you when the instructor accepts. If they decline, you get your payment or credit back.", null, "Email", null, "booking.requested.customer", true, "Request received: {{serviceTitle}} with {{instructorName}} on {{sessionDate}}" },
                    { new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000009"), "Hi {{instructorName}},\n\n{{customerName}} asked to book {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.\nBooking reference: {{bookingRef}}\n\nOpen your bookings to accept or decline the request.", null, "Email", null, "booking.requested.instructor", true, "New booking request: {{serviceTitle}} on {{sessionDate}}" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000008"));

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000009"));

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("8f0c2a51-3b7e-4c1d-9a20-000000000002"),
                columns: new[] { "Body", "Subject" },
                values: new object[] { "Hi {{instructorName}},\n\n{{customerName}} booked {{serviceTitle}} on {{sessionDate}} at {{sessionTime}}.\nBooking reference: {{bookingRef}}\n\nOpen your bookings to accept or decline the request.", "New booking: {{serviceTitle}} on {{sessionDate}}" });
        }
    }
}

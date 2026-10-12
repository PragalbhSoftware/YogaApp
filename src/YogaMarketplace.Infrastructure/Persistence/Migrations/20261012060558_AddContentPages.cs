using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentPages",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPages", x => x.Slug);
                    table.ForeignKey(
                        name: "FK_ContentPages_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ContentPages",
                columns: new[] { "Slug", "Body", "Title", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[,]
                {
                    { "privacy", "We collect only what we need to run bookings.\n\n## What we collect\n- Your name, phone number and gender, so instructors know who is coming.\n- Your visit address for home sessions, shared only with the instructor you book.\n- Payment references from Razorpay. We never see or store your card or UPI details.\n\n## How we use it\n- To create and manage your bookings, send updates and handle refunds.\n- To keep the marketplace safe, for example by blocking misuse.\n\n## Instructor documents\nCertificates uploaded by instructors are private. Only our team can open them to verify the instructor.\n\n## Your choices\nContact us to correct or delete your details. Some booking and payment records are kept as required by law.", "Privacy policy", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "refund-policy", "You pay when you book. The instructor then accepts or declines your request.\n\n## Full refunds\n- The instructor declines your request.\n- You cancel before the instructor accepts.\n- You cancel before the free-cancellation window closes. The booking page shows the exact time.\n- We cancel the session.\n\n## Late cancellations\nIf you cancel after the free-cancellation window closes, a late-cancellation fee is kept and paid to the instructor. The convenience fee is not refunded on a late cancel. The rest is refunded.\n\n## No-shows\nIf you miss a session without cancelling, no refund is given.\n\n## How refunds reach you\nRefunds go back to the original payment method through Razorpay. Banks usually take 5 to 7 working days.", "Refund policy", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 },
                    { "terms", "These terms cover your use of the marketplace, where customers book sessions with independent instructors.\n\n## Bookings\n- A booking is a request until the instructor accepts it.\n- Prices, fees and cancellation terms are shown before you pay and stay fixed for that booking.\n- Instructors are independent professionals and are responsible for the sessions they deliver.\n\n## Your account\n- Sign in with your own phone number and keep access to it secure.\n- Do not misuse the service, book on behalf of others without consent, or post misleading reviews.\n\n## Changes\nWe may update these terms. The date at the bottom of this page shows the latest version.", "Terms of use", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPages_UpdatedByUserId",
                table: "ContentPages",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPages");
        }
    }
}

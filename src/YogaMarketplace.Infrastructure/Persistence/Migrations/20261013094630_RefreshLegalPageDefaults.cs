using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Swaps the starter text of the refund and terms pages. A page an admin has saved is left alone, in both directions:
    /// the row changes only while it still holds the exact text being replaced.
    /// </summary>
    public partial class RefreshLegalPageDefaults : Migration
    {
        public const string OldRefund = "You pay when you book. The instructor then accepts or declines your request.\n\n## Full refunds\n- The instructor declines your request.\n- You cancel before the instructor accepts.\n- You cancel before the free-cancellation window closes. The booking page shows the exact time.\n- We cancel the session.\n\n## Late cancellations\nIf you cancel after the free-cancellation window closes, a late-cancellation fee is kept and paid to the instructor. The convenience fee is not refunded on a late cancel. The rest is refunded.\n\n## No-shows\nIf you miss a session without cancelling, no refund is given.\n\n## How refunds reach you\nRefunds go back to the original payment method through Razorpay. Banks usually take 5 to 7 working days.";

        public const string NewRefund = "You pay when you book. The instructor then accepts or declines your request.\n\n## How refund rules are set\nCancellation windows, late-cancellation fees, convenience fees and pack refunds follow the platform settings, which can change over time. The terms for your booking or pack are shown before you pay and stay fixed for that booking or pack.\n\n## Full refunds\n- The instructor declines your request.\n- You cancel before the instructor accepts.\n- You cancel before the free-cancellation window shown on your booking closes.\n- We cancel the session.\n\n## Late cancellations\nIf you cancel after the free-cancellation window closes, the late-cancellation fee shown on your booking is kept and paid to the instructor. The rest is refunded as shown on your booking.\n\n## No-shows\nIf you miss a session without cancelling, no refund is given.\n\n## How refunds reach you\nRefunds go back to the original payment method through Razorpay. How long the money takes to arrive depends on your bank.";

        public const string OldTerms = "These terms cover your use of the marketplace, where customers book sessions with independent instructors.\n\n## Bookings\n- A booking is a request until the instructor accepts it.\n- Prices, fees and cancellation terms are shown before you pay and stay fixed for that booking.\n- Instructors are independent professionals and are responsible for the sessions they deliver.\n\n## Your account\n- Sign in with your own phone number and keep access to it secure.\n- Do not misuse the service, book on behalf of others without consent, or post misleading reviews.\n\n## Changes\nWe may update these terms. The date at the bottom of this page shows the latest version.";

        public const string NewTerms = "These terms cover your use of the marketplace, where customers book sessions with independent instructors.\n\n## Bookings\n- A booking is a request until the instructor accepts it.\n- Prices, fees and cancellation terms are shown before you pay and stay fixed for that booking.\n- Instructors are independent professionals and are responsible for the sessions they deliver.\n\n## Your account\n- Sign in with your own phone number and keep access to it secure.\n- Do not misuse the service, book on behalf of others without consent, or post misleading reviews.\n\n## Off-platform bookings\n[Placeholder: the rule for arranging or paying for sessions outside the marketplace with an instructor you found here, and what happens if it is broken. Replace this text before launch.]\n\n## Changes\nWe may update these terms. The date at the bottom of this page shows the latest version.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReplaceUnedited(migrationBuilder, "refund-policy", OldRefund, NewRefund);
            ReplaceUnedited(migrationBuilder, "terms", OldTerms, NewTerms);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ReplaceUnedited(migrationBuilder, "refund-policy", NewRefund, OldRefund);
            ReplaceUnedited(migrationBuilder, "terms", NewTerms, OldTerms);
        }

        private static void ReplaceUnedited(MigrationBuilder migrationBuilder, string slug, string from, string to)
        {
            // SQLite has no N'' literal; SQL Server needs it to keep non-Latin characters.
            var prefix = migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer" ? "N" : "";
            string Quote(string value) => prefix + "'" + value.Replace("'", "''") + "'";
            migrationBuilder.Sql(
                $"UPDATE ContentPages SET Body = {Quote(to)} WHERE Slug = {Quote(slug)} AND Body = {Quote(from)};");
        }
    }
}

using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

/// <summary>Starter text for the public pages. Admin should replace it with reviewed wording before launch.</summary>
internal static class ContentPageDefaults
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public static IEnumerable<ContentPage> All() =>
    [
        Page(ContentPageRules.RefundPolicy, "Refund policy", """
            You pay when you book. The instructor then accepts or declines your request.

            ## How refund rules are set
            Cancellation windows, late-cancellation fees, convenience fees and pack refunds follow the platform settings, which can change over time. The terms for your booking or pack are shown before you pay and stay fixed for that booking or pack.

            ## Full refunds
            - The instructor declines your request.
            - You cancel before the instructor accepts.
            - You cancel before the free-cancellation window shown on your booking closes.
            - We cancel the session.

            ## Late cancellations
            If you cancel after the free-cancellation window closes, the late-cancellation fee shown on your booking is kept and paid to the instructor. The rest is refunded as shown on your booking.

            ## No-shows
            If you miss a session without cancelling, no refund is given.

            ## How refunds reach you
            Refunds go back to the original payment method through Razorpay. How long the money takes to arrive depends on your bank.
            """),
        Page(ContentPageRules.Terms, "Terms of use", """
            These terms cover your use of the marketplace, where customers book sessions with independent instructors.

            ## Bookings
            - A booking is a request until the instructor accepts it.
            - Prices, fees and cancellation terms are shown before you pay and stay fixed for that booking.
            - Instructors are independent professionals and are responsible for the sessions they deliver.

            ## Your account
            - Sign in with your own phone number and keep access to it secure.
            - Do not misuse the service, book on behalf of others without consent, or post misleading reviews.

            ## Off-platform bookings
            [Placeholder: the rule for arranging or paying for sessions outside the marketplace with an instructor you found here, and what happens if it is broken. Replace this text before launch.]

            ## Changes
            We may update these terms. The date at the bottom of this page shows the latest version.
            """),
        Page(ContentPageRules.Privacy, "Privacy policy", """
            We collect only what we need to run bookings.

            ## What we collect
            - Your name, phone number and gender, so instructors know who is coming.
            - Your visit address for home sessions, shared only with the instructor you book.
            - Payment references from Razorpay. We never see or store your card or UPI details.

            ## How we use it
            - To create and manage your bookings, send updates and handle refunds.
            - To keep the marketplace safe, for example by blocking misuse.

            ## Instructor documents
            Certificates uploaded by instructors are private. Only our team can open them to verify the instructor.

            ## Your choices
            Contact us to correct or delete your details. Some booking and payment records are kept as required by law.
            """),
    ];

    private static ContentPage Page(string slug, string title, string body) => new()
    {
        Slug = slug,
        Title = title,
        Body = body.Replace("\r\n", "\n").Trim(),
        Version = 1,
        UpdatedAt = SeededAt
    };
}

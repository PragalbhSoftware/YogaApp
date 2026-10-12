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

            ## Full refunds
            - The instructor declines your request.
            - You cancel before the instructor accepts.
            - You cancel before the free-cancellation window closes. The booking page shows the exact time.
            - We cancel the session.

            ## Late cancellations
            If you cancel after the free-cancellation window closes, a late-cancellation fee is kept and paid to the instructor. The convenience fee is not refunded on a late cancel. The rest is refunded.

            ## No-shows
            If you miss a session without cancelling, no refund is given.

            ## How refunds reach you
            Refunds go back to the original payment method through Razorpay. Banks usually take 5 to 7 working days.
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

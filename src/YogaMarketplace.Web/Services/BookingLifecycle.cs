using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

public static class InstructorBookingCommands
{
    public static bool CanAccept(string status) => status == BookingStatuses.PendingAccept;

    public static bool CanDecline(string status) => status == BookingStatuses.PendingAccept;

    public static bool CanComplete(string status) => status == BookingStatuses.Upcoming;
}

public static class CustomerReviews
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 1000;

    public static bool CanSubmit(string status, bool alreadyReviewed) =>
        status == BookingStatuses.Completed && !alreadyReviewed;

    public static bool IsInRange(int rating) => rating is >= MinRating and <= MaxRating;
}

public static class InstructorNotices
{
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Completed = "completed";

    public static string? ForStatus(string status) => status switch
    {
        BookingStatuses.Upcoming => Accepted,
        BookingStatuses.Declined => Declined,
        BookingStatuses.Completed => Completed,
        _ => null
    };

    public static string? Text(string? notice) => notice switch
    {
        Accepted => UiCopy.AcceptedNotice,
        Declined => UiCopy.DeclinedNotice,
        Completed => UiCopy.CompletedNotice,
        _ => null
    };
}

public static class BookingStatusText
{
    public static string FilterLabel(string status) => status switch
    {
        BookingStatuses.PendingAccept => UiCopy.FilterPending,
        BookingStatuses.Upcoming => UiCopy.FilterUpcoming,
        BookingStatuses.Declined => UiCopy.FilterDeclined,
        BookingStatuses.Completed => UiCopy.FilterCompleted,
        BookingStatuses.NoShow => UiCopy.FilterNoShow,
        BookingStatuses.Cancelled => UiCopy.FilterCancelled,
        _ => status
    };

    public static string Detail(string status) => status switch
    {
        BookingStatuses.PendingAccept => UiCopy.StatusPendingAccept,
        BookingStatuses.Upcoming => UiCopy.StatusUpcoming,
        BookingStatuses.Declined => UiCopy.StatusDeclined,
        BookingStatuses.Completed => UiCopy.StatusCompleted,
        BookingStatuses.NoShow => UiCopy.StatusNoShow,
        BookingStatuses.Cancelled => UiCopy.StatusCancelled,
        _ => status
    };
}

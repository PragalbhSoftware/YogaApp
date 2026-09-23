using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

public static class InstructorBookingCommands
{
    public static bool CanAccept(string status) => status == BookingStatuses.PendingAccept;

    public static bool CanDecline(string status) => status == BookingStatuses.PendingAccept;

    public static bool CanComplete(string status) => status == BookingStatuses.Upcoming;
}

public static class CustomerBookingChanges
{
    public static bool CanCancel(BookingDto booking) =>
        booking.Status is BookingStatuses.PendingAccept or BookingStatuses.Upcoming
        && !SessionClock.HasStarted(booking.Date, booking.Start);

    public static bool CanReschedule(BookingDto booking) =>
        booking.Status == BookingStatuses.Upcoming;
}

public static class RescheduleChoices
{
    public static IReadOnlyList<SlotDto> OpenFor(BookingDto booking, IEnumerable<SlotDto> slots) =>
        slots
            .Where(slot => slot.Id != booking.SlotId)
            .Where(slot => string.Equals(slot.Mode, booking.Mode, StringComparison.OrdinalIgnoreCase))
            .Where(slot => !SessionClock.HasEnded(slot.Date, slot.End))
            .OrderBy(slot => slot.Date)
            .ThenBy(slot => slot.Start, StringComparer.Ordinal)
            .ToList();
}

public static class CustomerNotices
{
    public const string Cancelled = "cancelled";
    public const string Rescheduled = "rescheduled";

    public static string? Text(string? notice) => notice switch
    {
        Cancelled => UiCopy.CancelledNotice,
        Rescheduled => UiCopy.RescheduledNotice,
        _ => null
    };
}

public static class CustomerReviews
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 1000;

    public static bool CanSubmit(string status, bool hasReviewed) =>
        status == BookingStatuses.Completed && !hasReviewed;

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

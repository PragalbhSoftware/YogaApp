using System.Globalization;
using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

/// <summary>
/// How an owned slot is shown. Occupying bookings come from the instructor inbox.
/// Blocking is the only edit; a blocked slot stays listed.
/// </summary>
public static class InstructorAvailability
{
    public const string Open = "open";
    public const string Blocked = "blocked";
    public const string Occupied = "occupied";
    public const string Past = "past";

    public static bool Occupies(string? status) =>
        status is BookingStatuses.PendingAccept
            or BookingStatuses.Upcoming
            or BookingStatuses.Completed
            or BookingStatuses.NoShow;

    public static string State(OwnedSlotDto slot, bool occupied)
    {
        if (occupied)
            return Occupied;
        if (slot.IsBlocked)
            return Blocked;
        if (slot.Date < SessionClock.Today())
            return Past;
        return Open;
    }

    public static bool CanBlock(OwnedSlotDto slot, bool occupied) =>
        State(slot, occupied) == Open;

    public static string Label(string state) => state switch
    {
        Blocked => UiCopy.SlotBlocked,
        Occupied => UiCopy.SlotOccupied,
        Past => UiCopy.SlotPast,
        _ => UiCopy.SlotOpen
    };

    public static string? Detail(string state) => state switch
    {
        Blocked => UiCopy.SlotBlockedDetail,
        Occupied => UiCopy.SlotOccupiedDetail,
        Past => UiCopy.SlotPastDetail,
        _ => null
    };

    public static string FormatDay(DateOnly date) =>
        date.ToString("dddd d MMM", CultureInfo.InvariantCulture);

    public static string WindowLabel(DateOnly from, DateOnly to) =>
        string.Format(
            CultureInfo.InvariantCulture,
            UiCopy.SlotRange,
            from.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            to.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
}

public static class InstructorAvailabilityNotices
{
    public const string Blocked = "blocked";

    public static string? Text(string? notice) => notice switch
    {
        Blocked => UiCopy.BlockedNotice,
        _ => null
    };
}

using System.Globalization;

namespace YogaMarketplace.Web.Services;

/// <summary>
/// Slot end times are Mumbai local, matching the API clock, so the book link is not offered after the session has ended.
/// </summary>
public static class SessionClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static bool HasEnded(DateOnly date, string end)
    {
        if (!TimeOnly.TryParseExact(end, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return false;

        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return new DateTimeOffset(utc, TimeSpan.Zero) <= DateTimeOffset.UtcNow;
    }

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
    }
}

namespace YogaMarketplace.Domain;

/// <summary>
/// Marketplace wall-clock. Slots are stored as Mumbai local date and time.
/// </summary>
public static class MumbaiClock
{
    public static TimeZoneInfo Zone { get; } = ResolveZone();

    public static DateOnly Today(DateTimeOffset? utcNow = null)
    {
        var utc = (utcNow ?? DateTimeOffset.UtcNow).UtcDateTime;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    }

    public static DateTimeOffset SessionStart(DateOnly date, TimeOnly start)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
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

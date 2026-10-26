namespace YogaMarketplace.Domain;

/// <summary>
/// Marketplace wall-clock. Slots are stored as a market-local date and time.
/// The zone id comes from platform settings. <see cref="DefaultTimeZoneId"/> is used until settings are loaded.
/// </summary>
public static class MarketClock
{
    public const string DefaultTimeZoneId = "Asia/Kolkata";
    public const string WindowsFallbackId = "India Standard Time";
    public const int TimeZoneIdMax = 64;

    private static TimeZoneInfo _zone = Resolve(DefaultTimeZoneId);

    public static string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    public static TimeZoneInfo Zone => _zone;

    public static void Use(string? timeZoneId)
    {
        var id = Normalize(timeZoneId);
        _zone = Resolve(id);
        TimeZoneId = id;
    }

    public static DateOnly Today(DateTimeOffset utcNow)
    {
        var utc = utcNow.UtcDateTime;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    }

    public static DateTimeOffset SessionStart(DateOnly date, TimeOnly start)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public static string Normalize(string? timeZoneId)
    {
        var id = (timeZoneId ?? "").Trim();
        if (id.Length is < 1 or > TimeZoneIdMax)
            throw new DomainException($"Use a known time zone id, for example {DefaultTimeZoneId}.");
        Resolve(id);
        return id;
    }

    private static TimeZoneInfo Resolve(string timeZoneId)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
            return zone;
        if (string.Equals(timeZoneId, DefaultTimeZoneId, StringComparison.Ordinal)
            && TimeZoneInfo.TryFindSystemTimeZoneById(WindowsFallbackId, out zone))
            return zone;
        throw new DomainException($"Use a known time zone id, for example {DefaultTimeZoneId}.");
    }
}

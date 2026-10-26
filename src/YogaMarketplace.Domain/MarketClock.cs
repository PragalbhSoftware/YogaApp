namespace YogaMarketplace.Domain;

/// <summary>The market zone for one API host. Settings load and save call <see cref="Use"/>.</summary>
public interface IMarketClock
{
    string TimeZoneId { get; }

    TimeZoneInfo Zone { get; }

    void Use(string? timeZoneId);

    DateOnly Today(DateTimeOffset utcNow);

    DateTimeOffset SessionStart(DateOnly date, TimeOnly start);
}

/// <summary>One host's zone. Other hosts in the same process keep their own instance.</summary>
public sealed class HostMarketClock : IMarketClock
{
    private TimeZoneInfo _zone;
    private string _timeZoneId;

    public HostMarketClock()
    {
        _timeZoneId = MarketClock.DefaultTimeZoneId;
        _zone = MarketClock.Resolve(_timeZoneId);
    }

    public string TimeZoneId => _timeZoneId;

    public TimeZoneInfo Zone => _zone;

    public void Use(string? timeZoneId)
    {
        var id = MarketClock.Normalize(timeZoneId);
        _zone = MarketClock.Resolve(id);
        _timeZoneId = id;
    }

    public DateOnly Today(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, _zone));

    public DateTimeOffset SessionStart(DateOnly date, TimeOnly start)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, _zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}

/// <summary>
/// Marketplace wall-clock. Slots are stored as a market-local date and time.
/// The zone id comes from platform settings. <see cref="DefaultTimeZoneId"/> is used until settings are loaded.
/// Static calls follow the host clock bound for the current request or job, otherwise the default zone.
/// </summary>
public static class MarketClock
{
    public const string DefaultTimeZoneId = "Asia/Kolkata";
    public const string WindowsFallbackId = "India Standard Time";
    public const int TimeZoneIdMax = 64;

    private static readonly AsyncLocal<IMarketClock?> Ambient = new();
    private static readonly IMarketClock Fallback = new HostMarketClock();

    public static string TimeZoneId => Active.TimeZoneId;

    public static TimeZoneInfo Zone => Active.Zone;

    public static DateOnly Today(DateTimeOffset utcNow) => Active.Today(utcNow);

    public static DateTimeOffset SessionStart(DateOnly date, TimeOnly start) => Active.SessionStart(date, start);

    /// <summary>Points static calls on this thread at one host's clock until the result is disposed.</summary>
    public static IDisposable Bind(IMarketClock clock)
    {
        var previous = Ambient.Value;
        Ambient.Value = clock;
        return new Restore(previous);
    }

    public static string Normalize(string? timeZoneId)
    {
        var id = (timeZoneId ?? "").Trim();
        if (id.Length is < 1 or > TimeZoneIdMax || (!IsIana(id) && !string.Equals(id, DefaultTimeZoneId, StringComparison.Ordinal)))
            throw new DomainException($"Use a known time zone id, for example {DefaultTimeZoneId}.");
        return id;
    }

    /// <summary>Public config id. A stored Windows id is converted; anything still unknown becomes the default.</summary>
    public static string ToPublicId(string? timeZoneId)
    {
        var id = (timeZoneId ?? "").Trim();
        if (IsIana(id))
            return id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) && IsIana(iana))
            return iana;
        return DefaultTimeZoneId;
    }

    internal static TimeZoneInfo Resolve(string timeZoneId)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) && Accept(timeZoneId, zone))
            return zone;
        if (string.Equals(timeZoneId, DefaultTimeZoneId, StringComparison.Ordinal)
            && TimeZoneInfo.TryFindSystemTimeZoneById(WindowsFallbackId, out zone))
            return zone;
        throw new DomainException($"Use a known time zone id, for example {DefaultTimeZoneId}.");
    }

    private static IMarketClock Active => Ambient.Value ?? Fallback;

    private static bool IsIana(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) && zone.HasIanaId;

    private static bool Accept(string timeZoneId, TimeZoneInfo zone) =>
        zone.HasIanaId || string.Equals(timeZoneId, DefaultTimeZoneId, StringComparison.Ordinal);

    private sealed class Restore : IDisposable
    {
        private readonly IMarketClock? _previous;
        private bool _disposed;

        public Restore(IMarketClock? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Ambient.Value = _previous;
        }
    }
}

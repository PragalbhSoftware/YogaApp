namespace YogaMarketplace.Domain;

/// <summary>When a recurring job runs: off, daily at a market-clock time, or every N minutes.</summary>
public sealed record JobSchedule
{
    private JobSchedule(TimeOnly? dailyAt, int? everyMinutes)
    {
        DailyAt = dailyAt;
        EveryMinutes = everyMinutes;
    }

    public static readonly JobSchedule Off = new(null, null);

    public TimeOnly? DailyAt { get; }
    public int? EveryMinutes { get; }
    public bool IsOff => DailyAt is null && EveryMinutes is null;

    public static JobSchedule Daily(TimeOnly at) => new(at, null);

    public static JobSchedule Every(int minutes) => minutes >= 1
        ? new(null, minutes)
        : throw new ArgumentOutOfRangeException(nameof(minutes), "A recurring job runs at most once a minute.");

    /// <summary>Daily: the next market-clock moment at <see cref="DailyAt"/> after <paramref name="now"/>. Every: now plus the interval.</summary>
    public DateTimeOffset NextRunAfter(DateTimeOffset now)
    {
        if (DailyAt is TimeOnly at)
        {
            var today = MarketClock.Today(now);
            var run = MarketClock.SessionStart(today, at);
            return run > now ? run : MarketClock.SessionStart(today.AddDays(1), at);
        }
        if (EveryMinutes is int minutes)
            return now.AddMinutes(minutes);
        throw new InvalidOperationException("A job that is off has no next run.");
    }

    /// <summary>Whether a run already queued for <paramref name="runAt"/> still matches this schedule, so it need not move.</summary>
    public bool Fits(DateTimeOffset runAt, DateTimeOffset now) => DailyAt is not null
        ? runAt == NextRunAfter(now)
        : EveryMinutes is int minutes && runAt <= now.AddMinutes(minutes);
}

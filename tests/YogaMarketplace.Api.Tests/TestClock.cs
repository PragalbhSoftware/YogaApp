namespace YogaMarketplace.Api.Tests;

/// <summary>A clock that only moves when a test sets it. Register it in place of <see cref="TimeProvider.System"/>.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

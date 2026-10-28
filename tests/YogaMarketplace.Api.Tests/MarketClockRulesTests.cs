using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class MarketClockRulesTests
{
    [Fact]
    public void Default_zone_id_is_accepted()
    {
        Assert.Equal(MarketClock.DefaultTimeZoneId, MarketClock.Normalize(MarketClock.DefaultTimeZoneId));
        Assert.Equal(MarketClock.DefaultTimeZoneId, MarketClock.Normalize(" " + MarketClock.DefaultTimeZoneId + " "));
    }

    [Fact]
    public void Iana_ids_are_accepted()
    {
        Assert.Equal("Asia/Kolkata", MarketClock.Normalize(" Asia/Kolkata "));
        Assert.Equal("UTC", MarketClock.Normalize("UTC"));
        Assert.Equal("Etc/UTC", MarketClock.Normalize("Etc/UTC"));
    }

    [Fact]
    public void Windows_ids_are_rejected()
    {
        var rejected = Assert.Throws<DomainException>(() => MarketClock.Normalize("India Standard Time"));
        Assert.Contains(MarketClock.DefaultTimeZoneId, rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_ids_are_rejected()
    {
        Assert.Throws<DomainException>(() => MarketClock.Normalize("Not/AZone"));
        Assert.Throws<DomainException>(() => MarketClock.Normalize(" "));
    }

    [Fact]
    public void Public_id_is_always_iana()
    {
        Assert.Equal("Asia/Kolkata", MarketClock.ToPublicId("Asia/Kolkata"));
        Assert.Equal("UTC", MarketClock.ToPublicId("UTC"));
        var fromWindows = MarketClock.ToPublicId("India Standard Time");
        Assert.NotEqual("India Standard Time", fromWindows);
        Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById(fromWindows, out var zone) && zone.HasIanaId);
        Assert.Equal(MarketClock.DefaultTimeZoneId, MarketClock.ToPublicId("Not/AZone"));
    }
}

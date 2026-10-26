namespace YogaMarketplace.Api.Tests;

/// <summary>Tests that change the market zone do not run beside the rest of the suite.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MarketClockCollection
{
    public const string Name = "Market clock";
}

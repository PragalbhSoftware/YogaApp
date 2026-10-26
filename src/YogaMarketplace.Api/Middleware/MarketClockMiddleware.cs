using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Middleware;

/// <summary>Binds this host's market clock for the request so static clock calls use it.</summary>
public class MarketClockMiddleware
{
    private readonly RequestDelegate _next;

    public MarketClockMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IMarketClock clock)
    {
        using (MarketClock.Bind(clock))
            await _next(context);
    }
}

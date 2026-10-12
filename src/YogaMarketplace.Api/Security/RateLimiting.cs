using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace YogaMarketplace.Api.Security;

public class RateLimitOptions
{
    public const string Section = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Anonymous browse calls (instructors, slots, reviews, catalog) per client IP per minute.</summary>
    public int BrowsePerMinute { get; set; } = 120;

    /// <summary>OTP request, resend and verify calls per client IP per minute.</summary>
    public int OtpPerMinute { get; set; } = 10;
}

public static class RateLimitPolicies
{
    public const string Browse = "browse";
    public const string Otp = "otp";
    public const string RejectedMessage = "Too many requests. Wait a minute and try again.";
}

/// <summary>
/// Fixed one-minute windows per client IP. Limits are read per request, so a config change applies without a code change.
/// Behind a reverse proxy the client IP comes from forwarded headers (see ForwardedHeaders in Program).
/// </summary>
public static class RateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Section));
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                await context.HttpContext.Response.WriteAsJsonAsync(new { error = RateLimitPolicies.RejectedMessage }, cancellationToken);
            };
            limiter.AddPolicy(RateLimitPolicies.Browse, http => PerClient(http, RateLimitPolicies.Browse, o => o.BrowsePerMinute));
            limiter.AddPolicy(RateLimitPolicies.Otp, http => PerClient(http, RateLimitPolicies.Otp, o => o.OtpPerMinute));
        });
        return services;
    }

    private static RateLimitPartition<string> PerClient(HttpContext http, string policy, Func<RateLimitOptions, int> permits)
    {
        var options = http.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitOptions>>().CurrentValue;
        if (!options.Enabled)
            return RateLimitPartition.GetNoLimiter($"{policy}:off");

        var limit = Math.Max(1, permits(options));
        var client = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{limit}:{client}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }
}

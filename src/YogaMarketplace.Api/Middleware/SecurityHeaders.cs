using Microsoft.Net.Http.Headers;

namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// Browser hardening for every response. API JSON may carry phone numbers and payments, so it gets the
/// strict policy and is never cached unless an endpoint opts in with its own Cache-Control. The SPA's own
/// files get a policy that lets the app, Razorpay checkout and YouTube embeds run.
/// </summary>
public static class SecurityHeaders
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private const string Razorpay = "https://checkout.razorpay.com https://api.razorpay.com";
    private const string YouTube = "https://www.youtube-nocookie.com https://www.youtube.com";

    // MUI (Emotion) injects <style> tags at runtime, so styles need 'unsafe-inline'; scripts do not.
    public const string AppContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://checkout.razorpay.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob: https://cdn.razorpay.com; " +
        "font-src 'self' data:; " +
        $"connect-src 'self' {Razorpay} https://lumberjack.razorpay.com; " +
        $"frame-src {Razorpay} {YouTube}; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                Apply(context);
                return Task.CompletedTask;
            });
            return next(context);
        });
    }

    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/health");

    private static void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        var path = context.Request.Path;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.XFrameOptions] = "DENY";
        if (IsApiPath(path))
        {
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers[HeaderNames.ContentSecurityPolicy] = ApiContentSecurityPolicy;
        }
        else
        {
            // YouTube embeds fail without a referrer, and Razorpay's bank pages open as popups.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(self \"https://checkout.razorpay.com\" \"https://api.razorpay.com\")";
            headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
            // Swagger UI (never in Production) runs inline scripts.
            if (!path.StartsWithSegments("/swagger"))
                headers[HeaderNames.ContentSecurityPolicy] = AppContentSecurityPolicy;
        }
        if (!headers.ContainsKey(HeaderNames.CacheControl))
            headers[HeaderNames.CacheControl] = "no-store";
    }
}

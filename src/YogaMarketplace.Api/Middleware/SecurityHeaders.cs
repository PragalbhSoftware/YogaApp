using Microsoft.Net.Http.Headers;

namespace YogaMarketplace.Api.Middleware;

/// <summary>
/// Browser hardening for every API response. JSON may carry phone numbers and payments,
/// so it is never cached unless an endpoint opts in with its own Cache-Control.
/// </summary>
public static class SecurityHeaders
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool strictContentPolicy)
    {
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
                headers[HeaderNames.XFrameOptions] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                if (strictContentPolicy)
                    headers[HeaderNames.ContentSecurityPolicy] = ApiContentSecurityPolicy;
                if (!headers.ContainsKey(HeaderNames.CacheControl))
                    headers[HeaderNames.CacheControl] = "no-store";
                return Task.CompletedTask;
            });
            return next(context);
        });
    }
}

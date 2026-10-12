using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace YogaMarketplace.Api.Hosting;

/// <summary>
/// Serves the built SPA from wwwroot (copied there by <c>dotnet publish</c>) so the API and the app run as one
/// site. Client routes such as <c>/bookings/123</c> fall back to index.html; <c>/api</c> never does.
/// </summary>
public static class SpaHosting
{
    public const string IndexFile = "index.html";
    public const string IndexCacheControl = "no-cache";
    public const string AssetCacheControl = "public, max-age=31536000, immutable";

    /// <summary>Vite fingerprints every file under /assets, so a new build never reuses a cached URL.</summary>
    private const string AssetsPath = "/assets";

    public static IApplicationBuilder UseSpaFiles(this IApplicationBuilder app)
    {
        app.UseDefaultFiles();
        return app.UseStaticFiles(FileOptions());
    }

    public static IEndpointRouteBuilder MapSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        // More specific than the index.html fallback, so unknown API routes stay JSON 404s.
        endpoints.MapFallback("/api/{**path}", context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        endpoints.MapFallbackToFile(IndexFile, FileOptions());
        return endpoints;
    }

    private static StaticFileOptions FileOptions() => new() { OnPrepareResponse = SetCacheControl };

    private static void SetCacheControl(StaticFileResponseContext context)
    {
        var http = context.Context;
        http.Response.Headers[HeaderNames.CacheControl] = http.Request.Path.StartsWithSegments(AssetsPath)
            ? AssetCacheControl
            : IndexCacheControl;
    }
}

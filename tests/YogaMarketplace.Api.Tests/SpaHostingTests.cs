using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using YogaMarketplace.Api.Hosting;
using YogaMarketplace.Api.Middleware;

namespace YogaMarketplace.Api.Tests;

/// <summary>The API with a published SPA in wwwroot, as on the single-site Plesk deploy.</summary>
public sealed class SpaYogaApiFactory : YogaApiFactory
{
    public const string IndexMarker = "<div id=\"root\"></div>";
    public const string AssetPath = "/assets/index-abc123.js";

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"yoga-marketplace-wwwroot-{Guid.NewGuid():N}");

    public SpaYogaApiFactory()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, SpaHosting.IndexFile), $"<!doctype html><html><body>{IndexMarker}</body></html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app');");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseWebRoot(_webRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        try
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public class SpaHostingTests : IClassFixture<SpaYogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SpaYogaApiFactory _factory;

    public SpaHostingTests(SpaYogaApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/bookings/123")]
    [InlineData("/admin/transactions")]
    public async Task Client_routes_serve_index_html_without_caching(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(SpaYogaApiFactory.IndexMarker, await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoCache);
        Assert.False(response.Headers.CacheControl.NoStore);
    }

    [Fact]
    public async Task Unknown_api_routes_return_404_json_not_index_html()
    {
        var response = await _factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);
        Assert.Equal("Not found.", body!.Error);
    }

    [Fact]
    public async Task Known_api_routes_still_reach_their_controllers()
    {
        var response = await _factory.CreateClient().GetAsync("/api/pages/terms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Fingerprinted_assets_are_cached_for_a_year()
    {
        var response = await _factory.CreateClient().GetAsync(SpaYogaApiFactory.AssetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SpaHosting.AssetCacheControl, response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Missing_files_are_404_not_index_html()
    {
        var response = await _factory.CreateClient().GetAsync("/assets/missing-file.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Html_gets_the_app_policy_and_api_keeps_the_strict_one()
    {
        var client = _factory.CreateClient();
        var page = await client.GetAsync("/bookings/123");
        var api = await client.GetAsync("/api/pages/terms");

        var pagePolicy = Header(page, "Content-Security-Policy");
        Assert.Equal(SecurityHeaders.AppContentSecurityPolicy, pagePolicy);
        Assert.DoesNotContain("default-src 'none'", pagePolicy, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' https://checkout.razorpay.com", pagePolicy, StringComparison.Ordinal);
        Assert.Contains("https://api.razorpay.com", pagePolicy, StringComparison.Ordinal);
        Assert.Contains("https://www.youtube-nocookie.com", pagePolicy, StringComparison.Ordinal);
        Assert.Contains("img-src 'self' data: blob: https://cdn.razorpay.com;", pagePolicy, StringComparison.Ordinal);
        Assert.Equal(SecurityHeaders.ApiContentSecurityPolicy, Header(api, "Content-Security-Policy"));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";

    private sealed record ErrorBody(string Error, string? TraceId);
}

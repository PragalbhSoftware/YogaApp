using Microsoft.Extensions.Configuration;
using YogaMarketplace.Api.Options;

namespace YogaMarketplace.Api.Tests;

public class CorsTests : IClassFixture<YogaApiFactory>
{
    private const string SpaOrigin = "http://localhost:5173";
    private readonly YogaApiFactory _factory;

    public CorsTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preflight_from_the_configured_origin_is_allowed()
    {
        var response = await PreflightAsync(SpaOrigin);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(SpaOrigin, Assert.Single(values));
    }

    [Fact]
    public async Task Preflight_from_an_unknown_origin_gets_no_cors_header()
    {
        var response = await PreflightAsync("https://evil.example");
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Production_without_origins_fails_closed()
    {
        var empty = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => CorsSettings.AllowedOriginsFrom(empty, isProduction: true));
        Assert.Empty(CorsSettings.AllowedOriginsFrom(empty, isProduction: false));
    }

    [Fact]
    public void Origins_are_trimmed_and_deduplicated()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = " https://app.example.in/ ",
                ["Cors:AllowedOrigins:1"] = "https://APP.example.in",
                ["Cors:AllowedOrigins:2"] = " "
            })
            .Build();

        Assert.Equal(new[] { "https://app.example.in" }, CorsSettings.AllowedOriginsFrom(config, isProduction: true));
    }

    private async Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/areas");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return await _factory.CreateClient().SendAsync(request);
    }
}

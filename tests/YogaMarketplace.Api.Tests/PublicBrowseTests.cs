using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Security;

namespace YogaMarketplace.Api.Tests;

public class PublicBrowseTests : IClassFixture<YogaApiFactory>
{
    private const string Ananya = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa4";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public PublicBrowseTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/providers")]
    [InlineData("/api/providers/" + Ananya)]
    [InlineData("/api/providers/" + Ananya + "/reviews")]
    [InlineData("/api/providers/" + Ananya + "/slots?mode=Home")]
    [InlineData("/api/areas")]
    [InlineData("/api/categories")]
    [InlineData("/api/site/banner")]
    public async Task Signed_out_visitors_can_browse(string url)
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Booking_still_needs_sign_in()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/bookings/quote?slotId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/bookings/orders", new { slotId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Browse_is_rate_limited_per_client_with_a_friendly_429()
    {
        await using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<RateLimitOptions>(options =>
            {
                options.Enabled = true;
                options.BrowsePerMinute = 3;
            })));
        var anon = limited.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/providers")).StatusCode);

        var blocked = await anon.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        var body = await blocked.Content.ReadFromJsonAsync<ErrorBody>(Json);
        Assert.Equal(RateLimitPolicies.RejectedMessage, body!.Error);

        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health")).StatusCode);
    }

    private sealed record ErrorBody(string Error);
}

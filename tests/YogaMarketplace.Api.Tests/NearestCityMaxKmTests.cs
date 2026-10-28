using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>Own host so changing NearestCityMaxKm does not leak into other catalog tests.</summary>
public class NearestCityMaxKmTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static int _seq;
    private readonly YogaApiFactory _factory;

    public NearestCityMaxKmTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Nearest_city_is_empty_past_the_configured_distance()
    {
        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = Word(), stateId = state.Id, displayOrder = 1 });
        await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Pinpoint", cityId = city.Id, pincode = "560010", latitude = 19.0m, longitude = 72.0m });

        try
        {
            var anon = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.NoContent, (await anon.GetAsync("/api/catalog/cities/nearest?lat=20.5&lng=72")).StatusCode);
            var close = await anon.GetFromJsonAsync<CityBody>("/api/catalog/cities/nearest?lat=19&lng=72", Json);
            Assert.Equal(city.Id, close!.Id);

            using var current = JsonDocument.Parse(await admin.GetStringAsync("/api/admin/settings"));
            var version = current.RootElement.GetProperty("version").GetInt32();
            var rejected = await admin.PatchAsJsonAsync("/api/admin/settings", new { version, nearestCityMaxKm = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var tooFar = await admin.PatchAsJsonAsync("/api/admin/settings", new { version, nearestCityMaxKm = 501 });
            Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);

            var widened = await admin.PatchAsJsonAsync("/api/admin/settings", new { version, nearestCityMaxKm = 500 });
            Assert.True(widened.IsSuccessStatusCode, await widened.Content.ReadAsStringAsync());
            var farther = await anon.GetFromJsonAsync<CityBody>("/api/catalog/cities/nearest?lat=20.5&lng=72", Json);
            Assert.Equal(city.Id, farther!.Id);
        }
        finally
        {
            using var after = JsonDocument.Parse(await admin.GetStringAsync("/api/admin/settings"));
            var resetVersion = after.RootElement.GetProperty("version").GetInt32();
            var reset = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = resetVersion, nearestCityMaxKm = 100 });
            Assert.True(reset.IsSuccessStatusCode, await reset.Content.ReadAsStringAsync());
        }
    }

    private async Task<HttpClient> SignInAsync(string phone = SeedIds.AdminPhone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static string Word()
    {
        var n = Interlocked.Increment(ref _seq);
        return "Place" + (char)('a' + (n / 26) % 26) + (char)('a' + (n % 26));
    }

    private static string Code()
    {
        var n = Interlocked.Increment(ref _seq);
        return "Q" + (char)('A' + (n / 26) % 26) + (char)('A' + (n % 26));
    }

    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record StateBody(Guid Id);
    private sealed record CityBody(Guid Id);
    private sealed record AreaBody(Guid Id);
}

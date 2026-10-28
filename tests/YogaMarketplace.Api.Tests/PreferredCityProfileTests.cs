using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class PreferredCityProfileTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public PreferredCityProfileTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preferred_city_requires_sign_in()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/profile/preferred-city", new { preferredCityId = TestWorld.CityId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preferred_city_can_be_saved_and_cleared()
    {
        var client = await SignUpAsync();
        var empty = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Null(empty!.PreferredCityId);

        var saved = await PutAsync<ProfileBody>(client, "/api/profile/preferred-city", new { preferredCityId = TestWorld.CityId });
        Assert.Equal(TestWorld.CityId, saved.PreferredCityId);

        var again = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Equal(TestWorld.CityId, again!.PreferredCityId);

        var cleared = await PutAsync<ProfileBody>(client, "/api/profile/preferred-city", new { preferredCityId = (Guid?)null });
        Assert.Null(cleared.PreferredCityId);
    }

    [Fact]
    public async Task Preferred_city_rejects_unknown_and_inactive_cities()
    {
        var client = await SignUpAsync();
        var admin = await SignInAdminAsync();

        var unknown = await client.PutAsJsonAsync("/api/profile/preferred-city", new { preferredCityId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new
        {
            name = "Quiet" + Guid.NewGuid().ToString("N")[..6],
            stateId = TestWorld.StateId,
            displayOrder = 9
        });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = false });

        var inactive = await client.PutAsJsonAsync("/api/profile/preferred-city", new { preferredCityId = city.Id });
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Contains("not available", await inactive.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preferred_city_rejects_an_active_city_in_an_inactive_state()
    {
        var client = await SignUpAsync();
        var admin = await SignInAdminAsync();

        var tag = (char)('A' + Random.Shared.Next(26));
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new
        {
            name = "Dim State " + tag,
            code = "Z" + tag + (char)('A' + Random.Shared.Next(26))
        });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new
        {
            name = "Lamp City " + tag,
            stateId = state.Id,
            displayOrder = 1
        });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = false });
        await PatchAsync<StateBody>(admin, $"/api/admin/states/{state.Id}", new { isActive = false });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = true });

        var response = await client.PutAsJsonAsync("/api/profile/preferred-city", new { preferredCityId = city.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not available", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deleting_a_city_customers_prefer_is_blocked()
    {
        var admin = await SignInAdminAsync();
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new
        {
            name = "Spare" + Guid.NewGuid().ToString("N")[..6],
            stateId = TestWorld.StateId,
            displayOrder = 8
        });

        var customer = await SignUpAsync();
        await PutAsync<ProfileBody>(customer, "/api/profile/preferred-city", new { preferredCityId = city.Id });

        var blocked = await admin.DeleteAsync($"/api/admin/cities/{city.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("in use", await blocked.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        await PutAsync<ProfileBody>(customer, "/api/profile/preferred-city", new { preferredCityId = (Guid?)null });
        var gone = await admin.DeleteAsync($"/api/admin/cities/{city.Id}");
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_city_requires_admin_and_blocks_unknown_or_linked_rows()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync($"/api/admin/cities/{Guid.NewGuid()}")).StatusCode);

        var customer = await SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync($"/api/admin/cities/{Guid.NewGuid()}")).StatusCode);

        var admin = await SignInAdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/cities/{Guid.NewGuid()}")).StatusCode);

        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new
        {
            name = "Busy" + Guid.NewGuid().ToString("N")[..6],
            stateId = TestWorld.StateId,
            displayOrder = 7
        });
        await PostAsync<AreaBody>(admin, "/api/admin/areas", new
        {
            name = "Dock" + Guid.NewGuid().ToString("N")[..4],
            cityId = city.Id,
            pincode = "560099"
        });

        var blocked = await admin.DeleteAsync($"/api/admin/cities/{city.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("neighbourhood", await blocked.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> SignUpAsync()
    {
        var client = _factory.CreateClient();
        var phone = "+9197" + Random.Shared.Next(10000000, 99999999).ToString();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new
        {
            phone,
            name = "City Fan",
            gender = "Other",
            isNewUser = true,
            acceptedPolicies = true
        });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<HttpClient> SignInAdminAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AdminPhone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AdminPhone, code = otp.DevCode });
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

    private static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record StateBody(Guid Id, string Name, bool IsActive);
    private sealed record CityBody(Guid Id, string Name, Guid StateId, bool IsActive);
    private sealed record AreaBody(Guid Id);
    private sealed record ProfileBody(Guid Id, string? Name, string Role, Guid? PreferredCityId);
}

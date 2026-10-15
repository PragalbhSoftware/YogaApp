using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class StateCityApiTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static int _seq;
    private readonly YogaApiFactory _factory;

    public StateCityApiTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_calls_are_rejected()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/states")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/admin/states", new { name = "Aa", code = "Aa" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/cities")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/admin/cities", new { name = "Aa" })).StatusCode);
    }

    [Fact]
    public async Task Admin_can_manage_states_and_cities_and_public_lists_hide_inactive_rows()
    {
        var admin = await SignInAsync();
        var firstName = Word();
        var secondName = Word();
        var first = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = "  " + firstName.ToLowerInvariant() + " ", code = "ab", displayOrder = 2 });
        Assert.Equal(firstName, first.Name);
        Assert.Equal("AB", first.Code);
        Assert.True(first.IsActive);

        var duplicate = await admin.PostAsJsonAsync("/api/admin/states", new { name = firstName.ToLowerInvariant(), code = "cd" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("already exists", (await duplicate.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var second = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = secondName, code = "cd", displayOrder = 0 });
        var openStates = await _factory.CreateClient().GetFromJsonAsync<List<StateBody>>("/api/catalog/states", Json);
        var openOrder = openStates!.Select(row => row.Id).ToList();
        Assert.True(openOrder.IndexOf(second.Id) < openOrder.IndexOf(first.Id));
        var missingState = await admin.PostAsJsonAsync("/api/admin/cities", new { name = Word() });
        Assert.Equal(HttpStatusCode.BadRequest, missingState.StatusCode);
        Assert.Contains("active state", (await missingState.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var quiet = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = "ef" });
        await PatchAsync<StateBody>(admin, $"/api/admin/states/{quiet.Id}", new { isActive = false });
        var inactiveState = await admin.PostAsJsonAsync("/api/admin/cities", new { name = Word(), stateId = quiet.Id });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveState.StatusCode);
        Assert.Contains("active state", (await inactiveState.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var cityName = Word();
        var laterCity = Word();
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = " " + cityName.ToLowerInvariant() + " ", stateId = first.Id, displayOrder = 2 });
        Assert.Equal(cityName, city.Name);
        Assert.Equal(cityName.ToLowerInvariant(), city.Slug);
        Assert.Equal(first.Id, city.StateId);

        var sameName = await admin.PostAsJsonAsync("/api/admin/cities", new { name = cityName.ToLowerInvariant(), stateId = first.Id });
        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Contains("already exists", (await sameName.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var sameSlug = await admin.PostAsJsonAsync("/api/admin/cities", new { name = cityName, stateId = second.Id });
        Assert.Equal(HttpStatusCode.Conflict, sameSlug.StatusCode);
        Assert.Contains("slug", (await sameSlug.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var earlier = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = laterCity, stateId = first.Id, displayOrder = 0 });
        var renamed = await PatchAsync<CityBody>(admin, $"/api/admin/cities/{earlier.Id}", new { name = laterCity + " End" });
        Assert.Equal(laterCity + " End", renamed.Name);
        Assert.Equal(laterCity.ToLowerInvariant() + "-end", renamed.Slug);

        var blocked = await admin.PatchAsJsonAsync($"/api/admin/states/{first.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("cities", (await blocked.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var publicBefore = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={first.Id}", Json);
        Assert.Equal(new[] { renamed.Id, city.Id }, publicBefore!.Select(row => row.Id).ToArray());

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = false });
        var publicCities = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={first.Id}", Json);
        Assert.DoesNotContain(publicCities!, row => row.Id == city.Id);
        Assert.Contains(publicCities!, row => row.Id == renamed.Id);

        var adminCities = await admin.GetFromJsonAsync<PagedBody<CityBody>>($"/api/admin/cities?stateId={first.Id}", Json);
        Assert.Contains(adminCities!.Items, row => row.Id == city.Id && row.IsActive == false);

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{renamed.Id}", new { isActive = false });
        var stateOff = await PatchAsync<StateBody>(admin, $"/api/admin/states/{first.Id}", new { isActive = false });
        Assert.False(stateOff.IsActive);

        var publicStates = await _factory.CreateClient().GetFromJsonAsync<List<StateBody>>("/api/catalog/states", Json);
        Assert.DoesNotContain(publicStates!, row => row.Id == first.Id);
        Assert.Contains(publicStates!, row => row.Id == second.Id);

        var hidden = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={first.Id}", Json);
        Assert.Empty(hidden!);
        var noState = await _factory.CreateClient().GetAsync("/api/catalog/cities");
        Assert.Equal(HttpStatusCode.BadRequest, noState.StatusCode);
        var unknown = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={Guid.NewGuid()}", Json);
        Assert.Empty(unknown!);

        var states = await admin.GetFromJsonAsync<PagedBody<StateBody>>("/api/admin/states", Json);
        Assert.Contains(states!.Items, row => row.Id == first.Id && row.IsActive == false);
    }

    private static string Word()
    {
        var n = Interlocked.Increment(ref _seq);
        return "Place" + (char)('a' + (n / 26) % 26) + (char)('a' + (n % 26));
    }

    private async Task<HttpClient> SignInAsync()
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

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record StateBody(Guid Id, string Name, string Code, bool IsActive, int DisplayOrder);
    private sealed record CityBody(Guid Id, string Name, Guid StateId, string Slug, bool IsActive, int DisplayOrder);
    private sealed record PagedBody<T>(List<T> Items, int Total, int Page, int PageSize);
}

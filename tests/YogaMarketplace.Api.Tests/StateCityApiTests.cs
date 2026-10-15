using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/admin/states/import-india", null)).StatusCode);
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
        Assert.Equal("ab-" + cityName.ToLowerInvariant(), city.Slug);
        Assert.Equal(first.Id, city.StateId);

        var sameName = await admin.PostAsJsonAsync("/api/admin/cities", new { name = cityName.ToLowerInvariant(), stateId = first.Id });
        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Contains("already exists", (await sameName.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var sameNameOtherState = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = cityName, stateId = second.Id });
        Assert.Equal("cd-" + cityName.ToLowerInvariant(), sameNameOtherState.Slug);

        var earlier = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = laterCity, stateId = first.Id, displayOrder = 0 });
        var renamed = await PatchAsync<CityBody>(admin, $"/api/admin/cities/{earlier.Id}", new { name = laterCity + " End" });
        Assert.Equal(laterCity + " End", renamed.Name);
        Assert.Equal("ab-" + laterCity.ToLowerInvariant() + "-end", renamed.Slug);

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

    [Fact]
    public async Task Changing_a_state_code_rewrites_city_slugs()
    {
        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = "ab" });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Harbor", stateId = state.Id });
        await PatchAsync<StateBody>(admin, $"/api/admin/states/{state.Id}", new { code = "xy" });
        var cities = await admin.GetFromJsonAsync<PagedBody<CityBody>>($"/api/admin/cities?stateId={state.Id}", Json);
        Assert.Equal("xy-harbor", cities!.Items.Single(row => row.Id == city.Id).Slug);
    }

    [Fact]
    public async Task Importing_india_locations_skips_what_is_already_there_and_stays_off()
    {
        var admin = await SignInAsync();
        var file = LocationFile();
        var keptName = file.States[0].Name;
        var other = file.States[1];
        var kept = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = keptName, code = "ZZ", displayOrder = 7 });
        var harbor = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Harbor", stateId = kept.Id });

        var first = await PostAsync<ImportBody>(admin, "/api/admin/states/import-india", new { });
        var second = await PostAsync<ImportBody>(admin, "/api/admin/states/import-india", new { });

        Assert.Equal(0, first.CitiesSkipped);
        Assert.True(first.StatesAdded > 0);
        Assert.True(first.CitiesAdded > 0);
        Assert.Equal(0, second.StatesAdded);
        Assert.Equal(0, second.CitiesAdded);
        Assert.Equal(first.StatesAdded + first.StatesSkipped, second.StatesSkipped);
        Assert.Equal(first.CitiesAdded + first.CitiesSkipped, second.CitiesSkipped);

        var states = await AllStatesAsync(admin);
        var keptRow = states.Single(row => row.Id == kept.Id);
        Assert.Equal("ZZ", keptRow.Code);
        Assert.Equal(7, keptRow.DisplayOrder);
        Assert.True(keptRow.IsActive);

        var keptCities = await AllCitiesAsync(admin, kept.Id);
        Assert.True(keptCities.Single(row => row.Id == harbor.Id).IsActive);
        Assert.Equal("zz-harbor", keptCities.Single(row => row.Id == harbor.Id).Slug);
        Assert.All(keptCities.Where(row => row.Id != harbor.Id), row => Assert.False(row.IsActive));

        var imported = states.Single(row => row.Name == other.Name);
        Assert.False(imported.IsActive);
        Assert.Equal(other.Code, imported.Code);
        var importedCities = await AllCitiesAsync(admin, imported.Id);
        Assert.NotEmpty(importedCities);
        Assert.All(importedCities, row => Assert.False(row.IsActive));
        Assert.All(importedCities, row => Assert.StartsWith(other.Code.ToLowerInvariant() + "-", row.Slug, StringComparison.Ordinal));

        var shared = file.States
            .SelectMany(state => state.Cities.Select(city => (state.Code, city)))
            .GroupBy(row => row.city, StringComparer.OrdinalIgnoreCase)
            .First(group => group.Select(row => row.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
        var sharedStates = states.Where(row => shared.Any(item => item.Code == row.Code)).ToList();
        var slugs = new List<string>();
        foreach (var state in sharedStates)
        {
            var match = (await AllCitiesAsync(admin, state.Id)).Single(row => string.Equals(row.Name, shared.Key, StringComparison.OrdinalIgnoreCase));
            Assert.False(match.IsActive);
            slugs.Add(match.Slug);
        }
        Assert.True(slugs.Count >= 2);
        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.Ordinal).Count());

        var open = await _factory.CreateClient().GetFromJsonAsync<List<StateBody>>("/api/catalog/states", Json);
        Assert.DoesNotContain(open!, row => row.Id == imported.Id);
        Assert.Contains(open!, row => row.Id == kept.Id);
        var openCities = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={imported.Id}", Json);
        Assert.Empty(openCities!);
        var keptOpen = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={kept.Id}", Json);
        Assert.Equal(new[] { harbor.Id }, keptOpen!.Select(row => row.Id).ToArray());
    }

    private LocationFileBody LocationFile()
    {
        var root = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().ContentRootPath;
        var json = File.ReadAllText(Path.Combine(root, "SeedData", "locations-in.json"));
        return JsonSerializer.Deserialize<LocationFileBody>(json, Json)!;
    }

    private static async Task<List<StateBody>> AllStatesAsync(HttpClient admin)
    {
        var all = new List<StateBody>();
        for (var page = 1; ; page++)
        {
            var batch = await admin.GetFromJsonAsync<PagedBody<StateBody>>($"/api/admin/states?page={page}&pageSize=100", Json);
            all.AddRange(batch!.Items);
            if (all.Count >= batch.Total)
                return all;
        }
    }

    private static async Task<List<CityBody>> AllCitiesAsync(HttpClient admin, Guid stateId)
    {
        var all = new List<CityBody>();
        for (var page = 1; ; page++)
        {
            var batch = await admin.GetFromJsonAsync<PagedBody<CityBody>>($"/api/admin/cities?stateId={stateId}&page={page}&pageSize=100", Json);
            all.AddRange(batch!.Items);
            if (all.Count >= batch.Total)
                return all;
        }
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
    private sealed record ImportBody(int StatesAdded, int StatesSkipped, int CitiesAdded, int CitiesSkipped);
    private sealed record LocationStateBody(string Name, string Code, List<string> Cities);
    private sealed record LocationFileBody(string Source, string Retrieved, List<LocationStateBody> States);
}

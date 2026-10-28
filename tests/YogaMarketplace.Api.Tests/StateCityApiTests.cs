using System.Net;
using Microsoft.EntityFrameworkCore;
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
    public async Task Customer_and_instructor_calls_are_forbidden()
    {
        var customer = _factory.CreateClient();
        var phone = "+9198" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(customer, "/api/auth/otp/request", new { phone, name = "Places Customer", gender = "Other", isNewUser = true, acceptedPolicies = true });
        var auth = await PostAsync<AuthBody>(customer, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        Assert.Equal("Customer", auth.User.Role);
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);

        foreach (var client in new[] { customer, instructor })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/states")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/states", new { name = Word(), code = Code() })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/cities", new { name = Word(), stateId = Guid.NewGuid() })).StatusCode);
            var import = await client.PostAsync("/api/admin/states/import-india", null);
            Assert.Equal(HttpStatusCode.Forbidden, import.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/areas", new { name = "Dock", cityId = Guid.NewGuid(), pincode = "560001" })).StatusCode);
            Assert.Contains("Admin access required", (await import.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Duplicate_state_code_is_a_conflict_on_create_and_update()
    {
        var admin = await SignInAsync();
        var code = Code();
        await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code });
        var created = await admin.PostAsJsonAsync("/api/admin/states", new { name = Word(), code = code.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        Assert.Contains("code", (await created.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var other = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var updated = await admin.PatchAsJsonAsync($"/api/admin/states/{other.Id}", new { code });
        Assert.Equal(HttpStatusCode.Conflict, updated.StatusCode);
    }

    [Fact]
    public async Task Area_needs_its_city_and_state_switched_on()
    {
        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var quietCity = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Docktown", stateId = state.Id });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{quietCity.Id}", new { isActive = false });

        var cityOff = await admin.PostAsJsonAsync("/api/admin/areas", new { name = "Dockside", cityId = quietCity.Id, pincode = "560001" });
        Assert.Equal(HttpStatusCode.BadRequest, cityOff.StatusCode);
        Assert.Contains("active city", (await cityOff.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{quietCity.Id}", new { isActive = true });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var row = await db.States.SingleAsync(s => s.Id == state.Id);
            row.IsActive = false;
            await db.SaveChangesAsync();
        }
        var stateOff = await admin.PostAsJsonAsync("/api/admin/areas", new { name = "Dockside", cityId = quietCity.Id, pincode = "560001" });
        Assert.Equal(HttpStatusCode.BadRequest, stateOff.StatusCode);
        Assert.Contains("active state", (await stateOff.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var badPin = await admin.PostAsJsonAsync("/api/admin/areas", new { name = "Dockside", cityId = quietCity.Id, pincode = "12" });
        Assert.Equal(HttpStatusCode.BadRequest, badPin.StatusCode);
    }

    [Fact]
    public async Task Browse_hides_home_and_studio_in_an_inactive_city_and_keeps_online()
    {
        var admin = await SignInAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var cityId = await db.Areas.Where(a => a.Id == SeedIds.AreaBandra).Select(a => a.CityId).SingleAsync();
        var city = await db.Cities.SingleAsync(c => c.Id == cityId);
        try
        {
            await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = false });
            var anon = _factory.CreateClient();
            var home = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?mode=Home", Json);
            var studio = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?mode=Studio", Json);
            var online = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?mode=Online", Json);
            Assert.DoesNotContain(home!, row => row.Id == SeedIds.AnanyaProviderId);
            Assert.DoesNotContain(studio!, row => row.Id == SeedIds.AnanyaProviderId);
            Assert.Contains(online!, row => row.Id == SeedIds.AnanyaProviderId);

            var adminList = await admin.GetFromJsonAsync<List<ProviderBody>>("/api/admin/providers?status=all", Json);
            Assert.Contains(adminList!, row => row.Id == SeedIds.AnanyaProviderId);
        }
        finally
        {
            await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = true });
        }
    }

    [Fact]
    public async Task Admin_can_manage_states_and_cities_and_public_lists_hide_inactive_rows()
    {
        var admin = await SignInAsync();
        var firstName = Word();
        var secondName = Word();
        var firstCode = Code();
        var secondCode = Code();
        var first = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = "  " + firstName.ToLowerInvariant() + " ", code = firstCode.ToLowerInvariant(), displayOrder = 2 });
        Assert.Equal(firstName, first.Name);
        Assert.Equal(firstCode, first.Code);
        Assert.True(first.IsActive);

        var duplicate = await admin.PostAsJsonAsync("/api/admin/states", new { name = firstName.ToLowerInvariant(), code = secondCode });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("already exists", (await duplicate.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var second = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = secondName, code = secondCode, displayOrder = 0 });
        var openStates = await _factory.CreateClient().GetFromJsonAsync<List<StateBody>>("/api/catalog/states", Json);
        var openOrder = openStates!.Select(row => row.Id).ToList();
        Assert.True(openOrder.IndexOf(second.Id) < openOrder.IndexOf(first.Id));
        var missingState = await admin.PostAsJsonAsync("/api/admin/cities", new { name = Word() });
        Assert.Equal(HttpStatusCode.BadRequest, missingState.StatusCode);
        Assert.Contains("active state", (await missingState.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var quiet = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        await PatchAsync<StateBody>(admin, $"/api/admin/states/{quiet.Id}", new { isActive = false });
        var inactiveState = await admin.PostAsJsonAsync("/api/admin/cities", new { name = Word(), stateId = quiet.Id });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveState.StatusCode);
        Assert.Contains("active state", (await inactiveState.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var cityName = Word();
        var laterCity = Word();
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = " " + cityName.ToLowerInvariant() + " ", stateId = first.Id, displayOrder = 2 });
        Assert.Equal(cityName, city.Name);
        Assert.Equal(firstCode.ToLowerInvariant() + "-" + cityName.ToLowerInvariant(), city.Slug);
        Assert.Equal(first.Id, city.StateId);

        var sameName = await admin.PostAsJsonAsync("/api/admin/cities", new { name = cityName.ToLowerInvariant(), stateId = first.Id });
        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Contains("already exists", (await sameName.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var sameNameOtherState = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = cityName, stateId = second.Id });
        Assert.Equal(secondCode.ToLowerInvariant() + "-" + cityName.ToLowerInvariant(), sameNameOtherState.Slug);

        var earlier = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = laterCity, stateId = first.Id, displayOrder = 0 });
        var renamed = await PatchAsync<CityBody>(admin, $"/api/admin/cities/{earlier.Id}", new { name = laterCity + " End" });
        Assert.Equal(laterCity + " End", renamed.Name);
        Assert.Equal(firstCode.ToLowerInvariant() + "-" + laterCity.ToLowerInvariant() + "-end", renamed.Slug);

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
        var noState = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>("/api/catalog/cities", Json);
        Assert.DoesNotContain(noState!, row => row.Id == city.Id || row.Id == renamed.Id);
        Assert.Contains(noState!, row => row.Id == sameNameOtherState.Id);
        var unknown = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>($"/api/catalog/cities?stateId={Guid.NewGuid()}", Json);
        Assert.Empty(unknown!);

        var states = await admin.GetFromJsonAsync<PagedBody<StateBody>>("/api/admin/states", Json);
        Assert.Contains(states!.Items, row => row.Id == first.Id && row.IsActive == false);
    }

    [Fact]
    public async Task Changing_a_state_code_rewrites_city_slugs()
    {
        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Harbor", stateId = state.Id });
        var next = Code();
        await PatchAsync<StateBody>(admin, $"/api/admin/states/{state.Id}", new { code = next });
        var cities = await admin.GetFromJsonAsync<PagedBody<CityBody>>($"/api/admin/cities?stateId={state.Id}", Json);
        Assert.Equal(next.ToLowerInvariant() + "-harbor", cities!.Items.Single(row => row.Id == city.Id).Slug);
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
        Assert.Equal(36, first.StatesAdded + first.StatesSkipped);
        Assert.Equal(784, first.CitiesAdded + first.CitiesSkipped);
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

    [Fact]
    public async Task City_with_an_area_can_be_renamed_and_turned_off()
    {
        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = Word(), stateId = state.Id });
        var area = await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Dockside", cityId = city.Id, pincode = "560001" });

        var nextName = city.Name + " West";
        var renamed = await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { name = nextName });
        Assert.Equal(nextName, renamed.Name);
        var areas = await admin.GetFromJsonAsync<List<AreaBody>>("/api/admin/areas", Json);
        var row = areas!.Single(item => item.Id == area.Id);
        Assert.Equal(city.Id, row.CityId);
        Assert.Equal(nextName, row.CityName);

        var off = await PatchAsync<CityBody>(admin, $"/api/admin/cities/{city.Id}", new { isActive = false });
        Assert.False(off.IsActive);
        var after = await admin.GetFromJsonAsync<List<AreaBody>>("/api/admin/areas", Json);
        Assert.Contains(after!, item => item.Id == area.Id && item.CityId == city.Id);
    }

    [Fact]
    public async Task Public_cities_without_a_state_skip_closed_places_and_follow_display_order()
    {
        var admin = await SignInAsync();
        var suffix = Word();
        var openState = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code(), displayOrder = 4 });
        var closedState = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code(), displayOrder = 5 });
        var later = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Zeta" + suffix, stateId = openState.Id, displayOrder = 2 });
        var earlier = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Alpha" + suffix, stateId = openState.Id, displayOrder = 1 });
        var sameOrderFirst = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Bravo" + suffix, stateId = openState.Id, displayOrder = 3 });
        var sameOrderSecond = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Charlie" + suffix, stateId = openState.Id, displayOrder = 3 });
        var quietCity = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Quiet" + suffix, stateId = openState.Id, displayOrder = 0 });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{quietCity.Id}", new { isActive = false });
        var buried = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = "Buried" + suffix, stateId = closedState.Id, displayOrder = 0 });
        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{buried.Id}", new { isActive = false });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var state = await db.States.SingleAsync(s => s.Id == closedState.Id);
            state.IsActive = false;
            var city = await db.Cities.SingleAsync(c => c.Id == buried.Id);
            city.IsActive = true;
            await db.SaveChangesAsync();
        }

        var listed = await _factory.CreateClient().GetFromJsonAsync<List<CityBody>>("/api/catalog/cities", Json);
        var mine = listed!.Where(row => row.StateId == openState.Id).Select(row => row.Id).ToArray();
        Assert.Equal(new[] { earlier.Id, later.Id, sameOrderFirst.Id, sameOrderSecond.Id }, mine);
        Assert.DoesNotContain(listed!, row => row.Id == quietCity.Id || row.Id == buried.Id);
    }

    [Fact]
    public async Task Nearest_city_uses_active_areas_that_have_both_coordinates()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/catalog/cities/nearest")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/catalog/cities/nearest?lat=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/catalog/cities/nearest?lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/catalog/cities/nearest?lat=91&lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/catalog/cities/nearest?lat=0&lng=181")).StatusCode);

        var admin = await SignInAsync();
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new { name = Word(), code = Code() });
        var near = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = Word(), stateId = state.Id, displayOrder = 1 });
        var far = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = Word(), stateId = state.Id, displayOrder = 2 });
        var partial = await PostAsync<CityBody>(admin, "/api/admin/cities", new { name = Word(), stateId = state.Id, displayOrder = 0 });
        await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Nearpoint", cityId = near.Id, pincode = "560001", latitude = 19.1m, longitude = 72.8m });
        await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Farpoint", cityId = far.Id, pincode = "560002", latitude = 28.6m, longitude = 77.2m });
        var closed = await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Closedpoint", cityId = far.Id, pincode = "560003", latitude = 19.1m, longitude = 72.8m });
        await PatchAsync<AreaBody>(admin, $"/api/admin/areas/{closed.Id}", new { isActive = false });
        await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Halflat", cityId = partial.Id, pincode = "560004", latitude = 19.1m });

        var found = await anon.GetFromJsonAsync<CityBody>($"/api/catalog/cities/nearest?lat=19.1&lng=72.8", Json);
        Assert.Equal(near.Id, found!.Id);
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

    private static string Code()
    {
        var n = Interlocked.Increment(ref _seq);
        return "Q" + (char)('A' + (n / 26) % 26) + (char)('A' + (n % 26));
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

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record ProviderBody(Guid Id);
    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record StateBody(Guid Id, string Name, string Code, bool IsActive, int DisplayOrder);
    private sealed record CityBody(Guid Id, string Name, Guid StateId, string Slug, bool IsActive, int DisplayOrder);
    private sealed record AreaBody(Guid Id, Guid CityId, string CityName, string Name, bool IsActive);
    private sealed record PagedBody<T>(List<T> Items, int Total, int Page, int PageSize);
    private sealed record ImportBody(int StatesAdded, int StatesSkipped, int CitiesAdded, int CitiesSkipped);
    private sealed record LocationStateBody(string Name, string Code, List<string> Cities);
    private sealed record LocationFileBody(string Source, string Retrieved, List<LocationStateBody> States);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>Each test owns a factory so online cities / Pending providers never leak across facts.</summary>
public class ProviderBrowseCityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task City_filter_includes_in_city_and_online_providers_once()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var other = await CreateOnlineOnlyCityAsync(factory, admin, "Other");

        var anon = factory.CreateClient();
        var list = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga", Json);
        Assert.Contains(list!, p => p.Id == SeedIds.AnanyaProviderId && !p.IsOutOfCity);
        Assert.Contains(list!, p => p.Id == other.ProviderId && p.IsOutOfCity);
        Assert.Equal(1, list!.Count(p => p.Id == SeedIds.AnanyaProviderId));

        var home = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&mode=Home&category=yoga", Json);
        Assert.Contains(home!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.DoesNotContain(home!, p => p.Id == other.ProviderId);

        var studio = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&mode=Studio&category=yoga", Json);
        Assert.Contains(studio!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.DoesNotContain(studio!, p => p.Id == other.ProviderId);

        var online = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={other.CityId}&mode=Online&category=yoga", Json);
        Assert.Contains(online!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.Contains(online!, p => p.Id == other.ProviderId);
    }

    [Fact]
    public async Task Area_from_another_city_is_rejected_and_bad_city_is_rejected()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var other = await CreateOnlineOnlyCityAsync(factory, admin, "Away");

        var anon = factory.CreateClient();
        var wrongArea = await anon.GetAsync($"/api/providers?cityId={TestWorld.CityId}&areaId={other.AreaId}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongArea.StatusCode);
        Assert.Contains("\"code\":\"area_invalid\"", await wrongArea.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var areaAlone = await anon.GetAsync($"/api/providers?areaId={SeedIds.AreaBandra}");
        Assert.Equal(HttpStatusCode.BadRequest, areaAlone.StatusCode);
        Assert.Contains("Pick a city", await areaAlone.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var unknown = await anon.GetAsync($"/api/providers?cityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("Pick a city", await unknown.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var invalid = await anon.GetAsync("/api/providers?cityId=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{other.CityId}", new { isActive = false });
        var inactive = await anon.GetAsync($"/api/providers?cityId={other.CityId}");
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
    }

    [Fact]
    public async Task Area_narrows_in_city_but_keeps_out_of_city_online()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var other = await CreateOnlineOnlyCityAsync(factory, admin, "Remote");

        var anon = factory.CreateClient();
        var bandra = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&areaId={SeedIds.AreaBandra}&category=yoga", Json);
        Assert.Contains(bandra!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.Contains(bandra!, p => p.Id == other.ProviderId && p.IsOutOfCity);

        // Ananya is Online as well, so the neighbourhood filter does not drop her.
        var andheri = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&areaId={SeedIds.AreaAndheri}&category=yoga", Json);
        Assert.Contains(andheri!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.Contains(andheri!, p => p.Id == other.ProviderId && p.IsOutOfCity);

        var andheriHome = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&areaId={SeedIds.AreaAndheri}&mode=Home&category=yoga", Json);
        Assert.DoesNotContain(andheriHome!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.DoesNotContain(andheriHome!, p => p.Id == other.ProviderId);
    }

    [Fact]
    public async Task Inactive_place_and_unlisted_providers_are_excluded_even_when_online()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var other = await CreateOnlineOnlyCityAsync(factory, admin, "Quiet");

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{other.CityId}", new { isActive = false });
        var anon = factory.CreateClient();
        var list = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga", Json);
        Assert.DoesNotContain(list!, p => p.Id == other.ProviderId);

        await PatchAsync<CityBody>(admin, $"/api/admin/cities/{other.CityId}", new { isActive = true });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var provider = await db.Providers.SingleAsync(p => p.Id == other.ProviderId);
            provider.Status = ProviderStatus.Pending;
            await db.SaveChangesAsync();
        }

        list = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga", Json);
        Assert.DoesNotContain(list!, p => p.Id == other.ProviderId);
    }

    [Fact]
    public async Task Inactive_area_and_inactive_state_exclude_online_providers()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var other = await CreateOnlineOnlyCityAsync(factory, admin, "Closed");

        await PatchAsync<AreaBody>(admin, $"/api/admin/areas/{other.AreaId}", new { isActive = false });
        var anon = factory.CreateClient();
        var afterArea = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga", Json);
        Assert.DoesNotContain(afterArea!, p => p.Id == other.ProviderId);

        await PatchAsync<AreaBody>(admin, $"/api/admin/areas/{other.AreaId}", new { isActive = true });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var state = await db.States.SingleAsync(s => s.Id == other.StateId);
            state.IsActive = false;
            await db.SaveChangesAsync();
        }
        var afterState = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga", Json);
        Assert.DoesNotContain(afterState!, p => p.Id == other.ProviderId);
    }

    [Fact]
    public async Task Paging_mixed_in_city_and_online_skips_nobody_and_repeats_nobody()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAdminAsync(factory);
        var extras = new List<Guid>();
        foreach (var label in new[] { "PageOne", "PageTwo", "PageThree" })
            extras.Add((await CreateOnlineOnlyCityAsync(factory, admin, label)).ProviderId);

        var anon = factory.CreateClient();
        var page1 = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga&page=1&pageSize=2", Json);
        var page2 = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga&page=2&pageSize=2", Json);
        Assert.Equal(2, page1!.Count);
        Assert.NotEmpty(page2!);
        Assert.Empty(page1.Select(p => p.Id).Intersect(page2.Select(p => p.Id)));

        var all = await anon.GetFromJsonAsync<List<ProviderBody>>(
            $"/api/providers?cityId={TestWorld.CityId}&category=yoga&pageSize=100", Json);
        foreach (var id in extras.Append(SeedIds.AnanyaProviderId))
            Assert.Contains(all!, p => p.Id == id);
    }

    private static async Task<(Guid CityId, Guid AreaId, Guid StateId, Guid ProviderId)> CreateOnlineOnlyCityAsync(
        YogaApiFactory factory,
        HttpClient admin,
        string label)
    {
        var tag = (char)('A' + Random.Shared.Next(26));
        var state = await PostAsync<StateBody>(admin, "/api/admin/states", new
        {
            name = label + " State " + tag,
            code = "Y" + tag + (char)('A' + Random.Shared.Next(26))
        });
        var city = await PostAsync<CityBody>(admin, "/api/admin/cities", new
        {
            name = label + " City " + tag,
            stateId = state.Id,
            displayOrder = 1
        });
        var area = await PostAsync<AreaBody>(admin, "/api/admin/areas", new
        {
            name = label + " Area " + tag,
            cityId = city.Id,
            pincode = "5600" + Random.Shared.Next(10, 99)
        });

        var providerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.Users.Add(new User
            {
                Id = userId,
                Phone = "+9198" + Random.Shared.Next(10000000, 99999999),
                Name = label + " Online",
                Role = UserRole.Provider,
                CreatedAt = now
            });
            db.Providers.Add(new Provider
            {
                Id = providerId,
                UserId = userId,
                Status = ProviderStatus.Verified,
                DisplayName = label + " Online " + tag,
                AreaId = area.Id,
                OffersHome = false,
                OffersStudio = false,
                OffersOnline = true,
                OnlineRate = 499m,
                GoogleMeetLink = "https://meet.google.com/xyz-abcd-efg",
                CreatedAt = now,
                ReviewedAt = now
            });
            db.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                CategoryId = SeedIds.YogaCategoryId,
                Title = "Online yoga",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        return (city.Id, area.Id, state.Id, providerId);
    }

    private static async Task<HttpClient> SignInAdminAsync(YogaApiFactory factory)
    {
        var client = factory.CreateClient();
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

    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record StateBody(Guid Id, string Name, bool IsActive);
    private sealed record CityBody(Guid Id, string Name, Guid StateId, bool IsActive);
    private sealed record AreaBody(Guid Id, string Name, bool IsActive);
    private sealed record ProviderBody(Guid Id, string DisplayName, string City, bool IsOutOfCity);
}

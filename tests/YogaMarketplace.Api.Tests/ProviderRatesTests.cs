using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ProviderRatesTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ProviderRatesTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Instructor_updates_offered_mode_rates_and_browse_shows_them()
    {
        var instructor = await InstructorClientAsync();
        try
        {
            var updated = await PatchAsync<ProviderSelfBody>(instructor, "/api/providers/me/rates", new
            {
                homeRate = 950m,
                studioRate = 800m,
                onlineRate = 650m
            });
            Assert.Equal(950m, updated.HomeRate);
            Assert.Equal(800m, updated.StudioRate);
            Assert.Equal(650m, updated.OnlineRate);

            var mine = await instructor.GetFromJsonAsync<ProviderSelfBody>("/api/providers/me", Json);
            Assert.Equal(950m, mine!.HomeRate);
            Assert.Equal(800m, mine.StudioRate);
            Assert.Equal(650m, mine.OnlineRate);

            var browse = await instructor.GetFromJsonAsync<List<ProviderBody>>("/api/providers?category=yoga", Json);
            var ananya = Assert.Single(browse!, p => p.DisplayName == "Ananya Desai");
            Assert.Contains(ananya.Modes, m => m.Mode == "Home" && m.Rate == 950m);
            Assert.Contains(ananya.Modes, m => m.Mode == "Studio" && m.Rate == 800m);
            Assert.Contains(ananya.Modes, m => m.Mode == "Online" && m.Rate == 650m);
        }
        finally
        {
            await RestoreAnanyaRatesAsync();
        }
    }

    [Fact]
    public async Task Instructor_cannot_set_a_rate_for_a_mode_they_do_not_offer()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Priya Nair", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var areas = await client.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var registered = await PostAsync<RegisterBody>(client, "/api/providers/register", new
        {
            displayName = "Priya Nair",
            age = 29,
            areaId = areas!.Single(a => a.Name == "Bandra").Id,
            offersHome = true,
            homeRate = 700
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);

        var studio = await client.PatchAsJsonAsync("/api/providers/me/rates", new { studioRate = 500m });
        Assert.Equal(HttpStatusCode.BadRequest, studio.StatusCode);
        Assert.Contains("Studio", (await studio.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var empty = await client.PatchAsJsonAsync("/api/providers/me/rates", new { });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("at least one rate", (await empty.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var invalid = await client.PatchAsJsonAsync("/api/providers/me/rates", new { homeRate = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var raised = await PatchAsync<ProviderSelfBody>(client, "/api/providers/me/rates", new { homeRate = 750m });
        Assert.Equal(750m, raised.HomeRate);
        Assert.Null(raised.StudioRate);
        Assert.Null(raised.OnlineRate);
    }

    [Fact]
    public async Task Customer_and_anonymous_cannot_update_instructor_rates()
    {
        var anon = await _factory.CreateClient().PatchAsJsonAsync("/api/providers/me/rates", new { homeRate = 900m });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Rahul Sharma", "Male");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var forbidden = await customer.PatchAsJsonAsync("/api/providers/me/rates", new { homeRate = 900m });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Contains("instructor", (await forbidden.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RestoreAnanyaRatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var provider = await db.Providers.SingleAsync(p => p.Id == SeedIds.AnanyaProviderId);
        provider.HomeRate = 899m;
        provider.StudioRate = 749m;
        provider.OnlineRate = 599m;
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> InstructorClientAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AnanyaPhone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AnanyaPhone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name, string gender)
    {
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var parsed = JsonSerializer.Deserialize<T>(payload, Json);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var parsed = JsonSerializer.Deserialize<T>(payload, Json);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static string NewPhone() => "+9195" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record ModeBody(string Mode, decimal Rate);
    private sealed record ProviderBody(Guid Id, string DisplayName, string Area, string Status, List<ModeBody> Modes);
    private sealed record ProviderSelfBody(
        Guid Id,
        string DisplayName,
        string Status,
        bool OffersHome,
        bool OffersStudio,
        bool OffersOnline,
        decimal? HomeRate,
        decimal? StudioRate,
        decimal? OnlineRate);
    private sealed record RegisterBody(ProviderSelfBody Provider, string Token);
}

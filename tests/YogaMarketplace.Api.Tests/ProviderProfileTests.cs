using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ProviderProfileTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ProviderProfileTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Instructor_updates_teaching_profile_and_cannot_drop_the_last_mode()
    {
        var instructor = await InstructorClientAsync();
        try
        {
            var updated = await PatchAsync<ProviderSelfBody>(instructor, "/api/providers/me", AnanyaPatch(
                displayName: "Ananya Studio",
                bio: "Slow Hatha in Andheri.",
                areaId: SeedIds.AreaAndheri,
                offersOnline: false));
            Assert.Equal("Ananya Studio", updated.DisplayName);
            Assert.Equal("Slow Hatha in Andheri.", updated.Bio);
            Assert.Equal("Andheri", updated.Area);
            Assert.False(updated.OffersOnline);
            Assert.Equal(950m, updated.HomeRate);

            var browse = await instructor.GetFromJsonAsync<List<ProviderBody>>("/api/providers?category=yoga", Json);
            var ananya = Assert.Single(browse!, p => p.DisplayName == "Ananya Studio");
            Assert.Equal("Andheri", ananya.Area);
            Assert.DoesNotContain(ananya.Modes, m => m.Mode == "Online");

            var emptyModes = await instructor.PatchAsJsonAsync("/api/providers/me", AnanyaPatch(
                offersHome: false,
                offersStudio: false,
                offersOnline: false));
            Assert.Equal(HttpStatusCode.BadRequest, emptyModes.StatusCode);
            Assert.Contains("at least one", (await emptyModes.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await RestoreAnanyaAsync();
        }
    }

    [Fact]
    public async Task Instructor_cannot_turn_off_a_mode_with_a_live_booking()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Diya Shah", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenHomeSlotAsync(customer);
        var order = await PostAsync<CheckoutBody>(customer, "/api/bookings/orders", new
        {
            slotId = slot,
            homeAddress = "14th Road, Bandra West",
            landmark = "Near the station"
        });
        await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = order.OrderId });

        var instructor = await InstructorClientAsync();
        try
        {
            var denied = await instructor.PatchAsJsonAsync("/api/providers/me", AnanyaPatch(offersHome: false));
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
            Assert.Contains("Home", (await denied.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await RestoreAnanyaAsync();
        }
    }

    [Fact]
    public async Task Customer_and_anonymous_cannot_patch_instructor_profile()
    {
        var anon = await _factory.CreateClient().PatchAsJsonAsync("/api/providers/me", AnanyaPatch());
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Rahul Sharma", "Male");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var forbidden = await customer.PatchAsJsonAsync("/api/providers/me", AnanyaPatch());
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    private static object AnanyaPatch(
        string displayName = "Ananya Desai",
        string? bio = "Teaches Hatha yoga (slow, posture-focused) and restorative yoga (gentle and restful).",
        Guid? areaId = null,
        bool offersHome = true,
        bool offersStudio = true,
        bool offersOnline = true) => new
    {
        displayName,
        age = 32,
        email = "ananya@example.com",
        areaId = areaId ?? SeedIds.AreaBandra,
        bio,
        offersHome,
        offersStudio,
        offersOnline,
        homeRate = 950m,
        studioRate = 749m,
        onlineRate = 599m,
        studioAddress = "Lotus Studio, Bandra West, Mumbai",
        googleMeetLink = "https://meet.google.com/abc-defg-hij"
    };

    private async Task RestoreAnanyaAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var provider = await db.Providers.Include(p => p.User).SingleAsync(p => p.Id == SeedIds.AnanyaProviderId);
        provider.DisplayName = "Ananya Desai";
        provider.Bio = "Teaches Hatha yoga (slow, posture-focused) and restorative yoga (gentle and restful).";
        provider.Age = 32;
        provider.AreaId = SeedIds.AreaBandra;
        provider.OffersHome = true;
        provider.OffersStudio = true;
        provider.OffersOnline = true;
        provider.HomeRate = 899m;
        provider.StudioRate = 749m;
        provider.OnlineRate = 599m;
        provider.StudioAddress = "Lotus Studio, Bandra West, Mumbai";
        provider.GoogleMeetLink = "https://meet.google.com/abc-defg-hij";
        provider.User!.Name = "Ananya Desai";
        provider.User.Email = "ananya@example.com";
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

    private async Task<Guid> FirstOpenHomeSlotAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        var slot = list!.Slots.OrderByDescending(s => s.Date).ThenByDescending(s => s.Start).FirstOrDefault();
        Assert.NotNull(slot);
        return slot!.Id;
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

    private static string NewPhone() => "+9194" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record ModeBody(string Mode, decimal Rate);
    private sealed record ProviderBody(Guid Id, string DisplayName, string Area, string Status, List<ModeBody> Modes);
    private sealed record ProviderSelfBody(
        string DisplayName,
        string? Bio,
        string Area,
        bool OffersHome,
        bool OffersStudio,
        bool OffersOnline,
        decimal? HomeRate);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
    private sealed record CheckoutBody(Guid CheckoutId, string OrderId);
    private sealed record BookingBody(Guid Id, string Status);
}

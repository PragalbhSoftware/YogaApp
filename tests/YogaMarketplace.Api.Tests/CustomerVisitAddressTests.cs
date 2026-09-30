using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class CustomerVisitAddressTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public CustomerVisitAddressTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_saves_a_visit_address_and_home_orders_can_reuse_it()
    {
        var anon = await _factory.CreateClient().GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Vishal Shah", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var empty = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Equal("Customer", empty!.Role);
        Assert.Equal("Vishal Shah", empty.Name);
        Assert.Equal("Male", empty.Gender);
        Assert.Null(empty.VisitAddress);

        var renamed = await PatchAsync<ProfileBody>(client, "/api/profile", new { name = "  Vishal S  ", gender = "Other" });
        Assert.Equal("Vishal S", renamed.Name);
        Assert.Equal("Other", renamed.Gender);

        var named = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Equal("Vishal S", named!.Name);
        Assert.Equal("Other", named.Gender);

        var missingName = await client.PatchAsJsonAsync("/api/profile", new { name = "A", gender = "Male" });
        Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);

        var missing = await client.PutAsJsonAsync("/api/profile/visit-address", new
        {
            line1 = "12 Hill Road",
            area = "Bandra West",
            city = "Mumbai",
            pin = "12",
            landmark = "Near the station"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var saved = await PutAsync<ProfileBody>(client, "/api/profile/visit-address", new
        {
            line1 = "  12 Hill Road  ",
            area = "Bandra West",
            city = "Mumbai",
            pin = "400050",
            landmark = "Near the station"
        });
        Assert.Equal("12 Hill Road", saved.VisitAddress!.Line1);
        Assert.Equal("Bandra West", saved.VisitAddress.Area);
        Assert.Equal("Mumbai", saved.VisitAddress.City);
        Assert.Equal("400050", saved.VisitAddress.Pin);
        Assert.Equal("Near the station", saved.VisitAddress.Landmark);
        Assert.Equal("12 Hill Road, Bandra West, Mumbai 400050", saved.VisitAddress.HomeAddress);

        var again = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Equal("12 Hill Road, Bandra West, Mumbai 400050", again!.VisitAddress!.HomeAddress);

        var slotId = await InsertHomeSlotAsync(new TimeOnly(21, 30));
        var withoutBody = await PostAsync<OrderBody>(client, "/api/bookings/orders", new { slotId });
        Assert.Equal(slotId, withoutBody.SlotId);
        Assert.Equal("Home", withoutBody.Mode);
    }

    [Fact]
    public async Task Instructor_cannot_save_a_customer_visit_address()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AnanyaPhone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AnanyaPhone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        Assert.Equal("Provider", profile!.Role);
        Assert.Null(profile.VisitAddress);

        var denied = await client.PutAsJsonAsync("/api/profile/visit-address", new
        {
            line1 = "12 Hill Road",
            area = "Bandra West",
            city = "Mumbai",
            pin = "400050",
            landmark = "Near the station"
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Home_order_without_an_address_fails_until_one_is_saved()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Meera Iyer", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slotId = await InsertHomeSlotAsync(new TimeOnly(21, 45));
        var missing = await client.PostAsJsonAsync("/api/bookings/orders", new { slotId });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("address", (await missing.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> InsertHomeSlotAsync(TimeOnly start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var id = Guid.NewGuid();
        db.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = id,
            ProviderId = SeedIds.AnanyaProviderId,
            Mode = SessionMode.Home,
            Date = MumbaiClock.Today().AddDays(6),
            StartTime = start,
            EndTime = start.AddHours(1)
        });
        await db.SaveChangesAsync();
        return id;
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

    private static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body);
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

    private static string NewPhone() => "+9197" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record VisitAddressBody(string Line1, string Area, string City, string Pin, string Landmark, string HomeAddress);
    private sealed record ProfileBody(Guid Id, string? Name, string Phone, string? Gender, string Role, VisitAddressBody? VisitAddress);
    private sealed record OrderBody(Guid SlotId, string Mode);
}

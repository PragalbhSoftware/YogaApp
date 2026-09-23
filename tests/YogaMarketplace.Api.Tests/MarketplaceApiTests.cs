using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class MarketplaceApiTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public MarketplaceApiTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_is_live()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_customer_needs_name_and_gender_then_can_sign_in()
    {
        var phone = NewPhone();
        var client = _factory.CreateClient();

        var missing = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = true });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Name", (await missing.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        var otp = await Post<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Rahul Sharma", gender = "Male", isNewUser = true });
        Assert.False(string.IsNullOrWhiteSpace(otp.DevCode));

        var wrong = otp.DevCode == "000000" ? "111111" : "000000";
        var rejected = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = wrong });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("incorrect", (await rejected.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var auth = await Post<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var me = await client.GetFromJsonAsync<UserBody>("/api/auth/me", Json);
        Assert.Equal("Rahul Sharma", me!.Name);
        Assert.Equal("Male", me.Gender);
        Assert.Equal("Customer", me.Role);
        Assert.Equal(phone, me.Phone);
    }

    [Fact]
    public async Task Existing_customer_signs_in_with_phone_only_and_resend_replaces_the_code()
    {
        var phone = NewPhone();
        var client = _factory.CreateClient();
        await Post<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Meera Iyer", gender = "Female", isNewUser = true });
        var first = await Post<OtpBody>(client, "/api/auth/otp/resend", new { phone });
        var second = await Post<OtpBody>(client, "/api/auth/otp/resend", new { phone });
        Assert.NotEqual(first.DevCode, second.DevCode);

        var stale = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = first.DevCode });
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);

        var auth = await Post<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = second.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var again = await Post<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var signedIn = await Post<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = again.DevCode });
        Assert.Equal("Meera Iyer", signedIn.User.Name);
        Assert.Equal("Female", signedIn.User.Gender);

        var unknown = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = NewPhone(), isNewUser = false });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Expired_code_does_not_create_a_session()
    {
        var phone = NewPhone();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            db.OtpChallenges.Add(new OtpChallenge
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                CodeHash = OtpHasher.Hash("123456", "test-otp-pepper"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                IsNewUser = true,
                PendingName = "Late User",
                PendingGender = Gender.Other,
                IntendedRole = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("expired", (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await UserExists(phone));
    }

    [Fact]
    public async Task Catalog_is_mumbai_yoga_with_tbd_policy()
    {
        var client = _factory.CreateClient();
        var areas = await client.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        Assert.NotNull(areas);
        Assert.Equal(
            new[] { "Andheri", "Bandra", "Dadar", "Juhu", "Lower Parel", "Powai", "Worli" },
            areas.Select(a => a.Name).OrderBy(n => n).ToArray());
        Assert.All(areas, a => Assert.Equal("Mumbai", a.City));

        var categories = await client.GetFromJsonAsync<List<CategoryBody>>("/api/categories", Json);
        Assert.Contains(categories!, c => c.Slug == "yoga" && c.Name == "Yoga");

        var policy = await client.GetFromJsonAsync<PolicyBody>("/api/policy", Json);
        Assert.Equal("INR", policy!.Currency);
        Assert.Equal(15m, policy.PlatformFeePercent);
        Assert.Contains("TBD", policy.PolicyNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_returns_verified_instructors_and_mode_specific_slots()
    {
        var client = _factory.CreateClient();
        var all = await client.GetFromJsonAsync<List<ProviderBody>>("/api/providers?category=yoga", Json);
        var ananya = Assert.Single(all!, p => p.DisplayName == "Ananya Desai");
        Assert.Equal("Verified", ananya.Status);
        Assert.Equal("Bandra", ananya.Area);
        Assert.Contains(ananya.Modes, m => m.Mode == "Home" && m.Rate == 899m);

        var andheri = await client.GetFromJsonAsync<List<ProviderBody>>("/api/providers?area=Andheri", Json);
        Assert.DoesNotContain(andheri!, p => p.DisplayName == "Ananya Desai");

        var detailJson = await client.GetStringAsync($"/api/providers/{ananya.Id}");
        Assert.Contains("Lotus Studio", detailJson, StringComparison.Ordinal);
        Assert.DoesNotContain("meet.google.com", detailJson, StringComparison.OrdinalIgnoreCase);

        var home = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{ananya.Id}/slots?mode=Home", Json);
        var online = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{ananya.Id}/slots?mode=Online", Json);
        Assert.Equal(14, home!.Slots.Count);
        Assert.Equal(14, online!.Slots.Count);
        Assert.All(home.Slots, s => Assert.Contains(s.Start, new[] { "07:00", "08:00" }));
        Assert.All(online.Slots, s => Assert.Contains(s.Start, new[] { "18:00", "19:00" }));
        Assert.Empty(home.Slots.Select(s => s.Id).Intersect(online.Slots.Select(s => s.Id)));

        var mine = await client.GetAsync("/api/providers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);
    }

    [Fact]
    public async Task Booked_slot_drops_out_of_that_mode_only()
    {
        Guid slotId = Guid.Empty;
        Guid bookingId = Guid.Empty;
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var slot = await db.AvailabilitySlots
                .Where(s => s.ProviderId == SeedIds.AnanyaProviderId && s.Mode == SessionMode.Home && s.StartTime == new TimeOnly(7, 0))
                .OrderBy(s => s.Date)
                .FirstAsync();
            slotId = slot.Id;
            var customer = new User
            {
                Id = Guid.NewGuid(),
                Phone = NewPhone(),
                Name = "Slot Hold",
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(customer);
            bookingId = Guid.NewGuid();
            db.Bookings.Add(new Booking
            {
                Id = bookingId,
                CustomerId = customer.Id,
                ProviderId = SeedIds.AnanyaProviderId,
                ServiceId = SeedIds.AnanyaServiceId,
                SlotId = slot.Id,
                Mode = SessionMode.Home,
                Status = BookingStatus.PendingAccept,
                Amount = 899m,
                HomeAddress = "Bandra West",
                Landmark = "Near the station",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var client = _factory.CreateClient();
            var home = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
            var online = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Online", Json);
            Assert.DoesNotContain(home!.Slots, s => s.Id == slotId);
            Assert.Equal(14, online!.Slots.Count);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            if (booking is not null)
                db.Bookings.Remove(booking);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Provider_register_stays_pending_until_approval_and_can_add_mode_slots()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Priya Nair", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var areas = await client.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var bandra = areas!.Single(a => a.Name == "Bandra");
        var tomorrow = MumbaiClock.Today().AddDays(1);

        var missingMeet = await client.PostAsJsonAsync("/api/providers/register", new
        {
            displayName = "Priya Nair",
            age = 29,
            areaId = bandra.Id,
            offersOnline = true,
            onlineRate = 500
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingMeet.StatusCode);

        var created = await client.PostAsJsonAsync("/api/providers/register", new
        {
            displayName = "Priya Nair",
            age = 29,
            areaId = bandra.Id,
            offersHome = true,
            homeRate = 700,
            studioAddress = "ignored when studio is off"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<RegisterBody>(Json);
        Assert.Equal("Pending", body!.Provider.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var listed = await client.GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json);
        Assert.DoesNotContain(listed!, p => p.Id == body.Provider.Id);
        var hidden = await client.GetAsync($"/api/providers/{body.Provider.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var slots = await Post<List<SlotBody>>(client, "/api/providers/me/slots", new
        {
            mode = "Home",
            slots = new[] { new { date = tomorrow.ToString("yyyy-MM-dd"), start = "06:30", end = "07:30" } }
        });
        Assert.Equal("06:30", Assert.Single(slots).Start);

        var publicSlots = await client.GetAsync($"/api/providers/{body.Provider.Id}/slots?mode=Home");
        Assert.Equal(HttpStatusCode.NotFound, publicSlots.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/providers/register", new
        {
            displayName = "Priya Nair",
            areaId = bandra.Id,
            offersHome = true,
            homeRate = 700
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Seeded_instructor_can_see_meet_link_on_her_own_profile()
    {
        var client = _factory.CreateClient();
        var otp = await Post<OtpBody>(client, "/api/auth/otp/request", new { phone = "9876543210", isNewUser = false });
        var auth = await Post<AuthBody>(client, "/api/auth/otp/verify", new { phone = "9876543210", code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var me = await client.GetFromJsonAsync<ProviderSelfBody>("/api/providers/me", Json);
        Assert.Equal("Verified", me!.Status);
        Assert.Contains("meet.google.com", me.GoogleMeetLink, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name, string gender)
    {
        var phone = NewPhone();
        var otp = await Post<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await Post<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private async Task<bool> UserExists(string phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.Users.AnyAsync(u => u.Phone == phone);
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var parsed = JsonSerializer.Deserialize<T>(payload, Json);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static string NewPhone() => "+9198" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record CategoryBody(Guid Id, string Name, string Slug);
    private sealed record PolicyBody(string Currency, decimal PlatformFeePercent, string PolicyNote);
    private sealed record ModeBody(string Mode, decimal Rate);
    private sealed record ProviderBody(Guid Id, string DisplayName, string Area, string Status, List<ModeBody> Modes);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
    private sealed record ProviderSelfBody(Guid Id, string Status, string? GoogleMeetLink);
    private sealed record RegisterBody(ProviderSelfBody Provider, string Token);
}

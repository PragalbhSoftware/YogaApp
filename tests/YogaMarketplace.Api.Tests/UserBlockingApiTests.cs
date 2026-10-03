using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class UserBlockingApiTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public UserBlockingApiTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Only_admins_block_a_reason_is_required_and_admins_cannot_be_blocked()
    {
        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Block Gate Customer");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var me = await customer.GetFromJsonAsync<MeBody>("/api/auth/me", Json);

        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        var forbidden = await instructor.PostAsJsonAsync($"/api/admin/users/{me!.Id}/block", new { reason = "Not an admin" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        var noReason = await admin.PostAsJsonAsync($"/api/admin/users/{me.Id}/block", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var missing = await admin.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/block", new { reason = "Nobody here" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var admins = await admin.GetFromJsonAsync<List<UserBody>>("/api/admin/users?role=Admin", Json);
        var adminUser = Assert.Single(admins!, user => user.Phone == SeedIds.AdminPhone);
        var blockAdmin = await admin.PostAsJsonAsync($"/api/admin/users/{adminUser.Id}/block", new { reason = "Should fail" });
        Assert.Equal(HttpStatusCode.BadRequest, blockAdmin.StatusCode);

        var unblockActive = await admin.PostAsJsonAsync($"/api/admin/users/{me.Id}/unblock", new { });
        Assert.Equal(HttpStatusCode.Conflict, unblockActive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Blocked_customer_loses_their_session_and_sign_in_until_unblocked()
    {
        var customer = _factory.CreateClient();
        var (token, phone) = await SignUpAsync(customer, "Blocked Customer");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await customer.GetFromJsonAsync<MeBody>("/api/auth/me", Json);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        var blocked = await PostAsync<UserDetailBody>(admin, $"/api/admin/users/{me!.Id}/block", new { reason = "  Repeated no-shows  " });
        Assert.True(blocked.IsBlocked);
        Assert.Equal("Repeated no-shows", blocked.BlockedReason);
        Assert.NotNull(blocked.BlockedAt);
        var blockedEntry = Assert.Single(blocked.BlockHistory);
        Assert.Equal("Blocked", blockedEntry.Action);
        Assert.False(string.IsNullOrWhiteSpace(blockedEntry.AdminName));

        var again = await admin.PostAsJsonAsync($"/api/admin/users/{me.Id}/block", new { reason = "Second time" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/bookings/me")).StatusCode);

        var anonymous = _factory.CreateClient();
        var request = await anonymous.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = false });
        Assert.Equal(HttpStatusCode.Forbidden, request.StatusCode);
        Assert.Contains("suspended", (await request.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        var verify = await anonymous.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "123456" });
        Assert.Equal(HttpStatusCode.Forbidden, verify.StatusCode);

        var listed = await admin.GetFromJsonAsync<List<UserBody>>($"/api/admin/users?q={phone[3..]}", Json);
        Assert.Contains(listed!, user => user.Id == me.Id && user.IsBlocked);

        var unblocked = await PostAsync<UserDetailBody>(admin, $"/api/admin/users/{me.Id}/unblock", new { reason = "Appeal accepted" });
        Assert.False(unblocked.IsBlocked);
        Assert.Null(unblocked.BlockedReason);
        Assert.Equal(new[] { "Unblocked", "Blocked" }, unblocked.BlockHistory.Select(entry => entry.Action).ToArray());
        Assert.Equal("Appeal accepted", unblocked.BlockHistory[0].Reason);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/auth/me")).StatusCode);
        var signIn = await anonymous.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = false });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Blocked_provider_leaves_browse_and_cannot_be_booked_until_unblocked()
    {
        var applicant = _factory.CreateClient();
        var (applicantToken, _) = await SignUpAsync(applicant, "Meera Blocked");
        applicant.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", applicantToken);
        var areas = await applicant.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var registered = await PostAsync<RegisterBody>(applicant, "/api/providers/register", new
        {
            displayName = "Meera Blocked",
            age = 30,
            areaId = areas!.First().Id,
            offersOnline = true,
            onlineRate = 600,
            googleMeetLink = "https://meet.google.com/meera-blocked"
        });
        var providerId = registered.Provider.Id;
        var providerUser = await applicant.GetFromJsonAsync<MeBody>("/api/auth/me", Json);

        var admin = await SignInAsync(SeedIds.AdminPhone);
        await PostAsync<JsonElement>(admin, $"/api/admin/providers/{providerId}/verify", new { });
        var slotId = await InsertOnlineSlotAsync(providerId);

        var publicClient = _factory.CreateClient();
        Assert.Contains(await BrowseAsync(publicClient), provider => provider.Id == providerId);

        await PostAsync<UserDetailBody>(admin, $"/api/admin/users/{providerUser!.Id}/block", new { reason = "Fake credentials" });

        Assert.DoesNotContain(await BrowseAsync(publicClient), provider => provider.Id == providerId);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/providers/{providerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/providers/{providerId}/slots")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/providers/{providerId}/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await applicant.GetAsync("/api/providers/me")).StatusCode);

        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Would Be Booker");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var order = await customer.PostAsJsonAsync("/api/bookings/orders", new { slotId });
        Assert.Equal(HttpStatusCode.NotFound, order.StatusCode);

        await PostAsync<UserDetailBody>(admin, $"/api/admin/users/{providerUser.Id}/unblock", new { });

        Assert.Contains(await BrowseAsync(publicClient), provider => provider.Id == providerId);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/providers/{providerId}")).StatusCode);
        var reopened = await customer.PostAsJsonAsync("/api/bookings/orders", new { slotId });
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    private async Task<List<ProviderBody>> BrowseAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json))!;

    private async Task<Guid> InsertOnlineSlotAsync(Guid providerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Mode = SessionMode.Online,
            Date = MumbaiClock.Today().AddDays(30),
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(8, 0),
            IsBlocked = false
        };
        db.AvailabilitySlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name)
    {
        var phone = "+9197" + Random.Shared.Next(10000000, 99999999).ToString();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender = "Female", isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record MeBody(Guid Id, string Phone, string Role);
    private sealed record AuthBody(string Token, MeBody User);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record ProviderBody(Guid Id, string DisplayName);
    private sealed record RegisterProviderBody(Guid Id, string Status);
    private sealed record RegisterBody(RegisterProviderBody Provider, string Token);
    private sealed record UserBody(Guid Id, string Phone, string Role, bool IsBlocked);
    private sealed record BlockEventBody(string Action, string? Reason, string? AdminName, DateTimeOffset CreatedAt);
    private sealed record UserDetailBody(
        Guid Id,
        bool IsBlocked,
        DateTimeOffset? BlockedAt,
        string? BlockedReason,
        List<BlockEventBody> BlockHistory);
}

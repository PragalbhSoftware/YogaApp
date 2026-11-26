using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ProfileBadgesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Ahead of the real clock (so issued JWTs stay valid) and at noon UTC so market-local now±1 minute
    /// stay on the same calendar day.
    /// </summary>
    private static readonly DateTimeOffset FixedNow =
        new(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(3).AddHours(12), TimeSpan.Zero);

    [Fact]
    public async Task Guest_gets_401()
    {
        await using var factory = new YogaApiFactory();
        await using var host = WithClock(factory);
        var response = await host.CreateClient().GetAsync("/api/profile/badges");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_counts_only_own_future_pending_accept()
    {
        await using var factory = new YogaApiFactory();
        await using var host = WithClock(factory);
        var now = host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (pastDate, pastStart) = LocalSlot(now, -1);
        var (futureDate, futureStart) = LocalSlot(now, 1);

        var customerA = await SignUpAsync(host, "Customer A", "Female");
        var customerB = await SignUpAsync(host, "Customer B", "Male");
        var customerAId = await UserIdAsync(host, customerA);
        var customerBId = await UserIdAsync(host, customerB);

        var futurePending = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart);
        var pastPending = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, pastDate, pastStart);
        var upcomingSlot = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(30));
        var declinedSlot = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(60));
        var cancelledSlot = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(90));
        var completedSlot = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(120));
        var noShowSlot = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(150));

        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, futurePending, BookingStatus.PendingAccept);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, pastPending, BookingStatus.PendingAccept);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, upcomingSlot, BookingStatus.Upcoming);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, declinedSlot, BookingStatus.Declined);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, cancelledSlot, BookingStatus.Cancelled);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, completedSlot, BookingStatus.Completed);
        await InsertBookingAsync(host, customerAId, SeedIds.AnanyaProviderId, noShowSlot, BookingStatus.NoShow);
        await InsertBookingAsync(host, customerBId, SeedIds.AnanyaProviderId,
            await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(180)),
            BookingStatus.PendingAccept);

        var a = await GetBadgesAsync(customerA);
        Assert.Equal(1, a.Bookings);
        Assert.Equal(0, a.Requests);

        var b = await GetBadgesAsync(customerB);
        Assert.Equal(1, b.Bookings);
        Assert.Equal(0, b.Requests);
    }

    [Fact]
    public async Task Instructor_counts_only_own_provider_future_pending_accept()
    {
        await using var factory = new YogaApiFactory();
        await using var host = WithClock(factory);
        var now = host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (pastDate, pastStart) = LocalSlot(now, -1);
        var (futureDate, futureStart) = LocalSlot(now, 1);

        var instructorA = await SignInAsync(host, SeedIds.AnanyaPhone);
        var instructorB = await RegisterInstructorAsync(host);
        var customer = await SignUpAsync(host, "Badge Customer", "Female");
        var customerId = await UserIdAsync(host, customer);
        var providerBId = await ProviderIdForUserAsync(host, await UserIdAsync(host, instructorB));
        var serviceBId = await ServiceIdForProviderAsync(host, providerBId);

        var aFuture = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart);
        var aPast = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, pastDate, pastStart);
        var aUpcoming = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(30));
        var aDeclined = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(60));
        var aCancelled = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(90));
        var aCompleted = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(120));
        var aNoShow = await InsertSlotAsync(host, SeedIds.AnanyaProviderId, futureDate, futureStart.AddMinutes(150));
        var bFuture = await InsertSlotAsync(host, providerBId, futureDate, futureStart.AddMinutes(10));

        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aFuture, BookingStatus.PendingAccept);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aPast, BookingStatus.PendingAccept);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aUpcoming, BookingStatus.Upcoming);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aDeclined, BookingStatus.Declined);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aCancelled, BookingStatus.Cancelled);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aCompleted, BookingStatus.Completed);
        await InsertBookingAsync(host, customerId, SeedIds.AnanyaProviderId, aNoShow, BookingStatus.NoShow);
        await InsertBookingAsync(host, customerId, providerBId, bFuture, BookingStatus.PendingAccept, serviceBId);

        var a = await GetBadgesAsync(instructorA);
        Assert.Equal(0, a.Bookings);
        Assert.Equal(1, a.Requests);

        var b = await GetBadgesAsync(instructorB);
        Assert.Equal(0, b.Bookings);
        Assert.Equal(1, b.Requests);
    }

    [Fact]
    public async Task Admin_gets_zero_bookings_and_requests()
    {
        await using var factory = new YogaApiFactory();
        await using var host = WithClock(factory);
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        var badges = await GetBadgesAsync(admin);
        Assert.Equal(0, badges.Bookings);
        Assert.Equal(0, badges.Requests);
    }

    [Fact]
    public async Task Instructor_without_provider_row_gets_requests_zero()
    {
        await using var factory = new YogaApiFactory();
        await using var host = WithClock(factory);
        var client = host.CreateClient();
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new
        {
            phone,
            name = "No Provider Yet",
            gender = "Other",
            isNewUser = true,
            acceptedPolicies = true
        });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var user = await db.Users.SingleAsync(u => u.Phone == phone);
            user.Role = UserRole.Provider;
            await db.SaveChangesAsync();
        }

        // Role is loaded from the DB by user id; the JWT role claim is not used for the count.
        var badges = await GetBadgesAsync(client);
        Assert.Equal(0, badges.Bookings);
        Assert.Equal(0, badges.Requests);
    }

    private static WebApplicationFactory<Program> WithClock(YogaApiFactory factory)
    {
        var clock = new TestClock(FixedNow);
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
    }

    private static (DateOnly Date, TimeOnly Start) LocalSlot(DateTimeOffset utcNow, int minuteOffset)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, MarketClock.Zone).AddMinutes(minuteOffset);
        return (DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
    }

    private static async Task<HttpClient> SignUpAsync(WebApplicationFactory<Program> host, string name, string gender)
    {
        var client = host.CreateClient();
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new
        {
            phone,
            name,
            gender,
            isNewUser = true,
            acceptedPolicies = true
        });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await TestPreferredCity.PreferHarborAsync(client);
        return client;
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string phone)
    {
        var client = host.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<HttpClient> RegisterInstructorAsync(WebApplicationFactory<Program> host)
    {
        var client = await SignUpAsync(host, "Other Instructor", "Female");
        var registered = await PostAsync<AuthBody>(client, "/api/providers/register", new
        {
            displayName = "Other Instructor",
            age = 34,
            areaId = SeedIds.AreaBandra,
            offersOnline = true,
            onlineRate = 500m,
            googleMeetLink = "https://meet.google.com/badge-other"
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        return client;
    }

    private static async Task<Guid> InsertSlotAsync(
        WebApplicationFactory<Program> host,
        Guid providerId,
        DateOnly date,
        TimeOnly start)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var id = Guid.NewGuid();
        db.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = id,
            ProviderId = providerId,
            Mode = SessionMode.Home,
            Date = date,
            StartTime = start,
            EndTime = start.AddMinutes(60),
            IsBlocked = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task InsertBookingAsync(
        WebApplicationFactory<Program> host,
        Guid customerId,
        Guid providerId,
        Guid slotId,
        BookingStatus status,
        Guid? serviceId = null)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProviderId = providerId,
            ServiceId = serviceId ?? SeedIds.AnanyaServiceId,
            SlotId = slotId,
            Mode = SessionMode.Home,
            Status = status,
            Amount = 899m,
            HomeAddress = "Test Address",
            Landmark = "Landmark",
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> UserIdAsync(WebApplicationFactory<Program> host, HttpClient client)
    {
        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/profile", Json);
        return profile!.Id;
    }

    private static async Task<Guid> ProviderIdForUserAsync(WebApplicationFactory<Program> host, Guid userId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.Providers.Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
    }

    private static async Task<Guid> ServiceIdForProviderAsync(WebApplicationFactory<Program> host, Guid providerId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.Services.Where(s => s.ProviderId == providerId).Select(s => s.Id).FirstAsync();
    }

    private static async Task<BadgesBody> GetBadgesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/profile/badges");
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonSerializer.Deserialize<BadgesBody>(json, Json)!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonSerializer.Deserialize<T>(json, Json)!;
    }

    private static string NewPhone() => $"+91{Random.Shared.NextInt64(7000000000L, 9999999999L)}";

    private sealed record BadgesBody(int Bookings, int Requests);
    private sealed record ProfileBody(Guid Id);
    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
}

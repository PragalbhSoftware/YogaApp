using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ProviderSlotEditTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ProviderSlotEditTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Instructor_lists_own_slots_including_blocked_and_hides_them_from_the_public_list()
    {
        var anon = await _factory.CreateClient().GetAsync("/api/providers/me/slots?mode=Home");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var instructor = await InstructorClientAsync();
        var today = MumbaiClock.Today();
        var windowEnd = today.AddDays(6);

        var mine = await instructor.GetFromJsonAsync<OwnedSlotListBody>(
            "/api/providers/me/slots?mode=home",
            Json);
        Assert.Equal("Home", mine!.Mode);
        Assert.Equal(today, mine.From);
        Assert.Equal(windowEnd, mine.To);
        Assert.Equal(14, mine.Slots.Count);
        Assert.All(mine.Slots, s => Assert.False(s.IsBlocked));

        var noMode = await instructor.GetAsync("/api/providers/me/slots");
        Assert.Equal(HttpStatusCode.BadRequest, noMode.StatusCode);
        var backwards = await instructor.GetAsync(
            $"/api/providers/me/slots?mode=Home&from={windowEnd:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);

        var day = mine.Slots.Max(s => s.Date);
        var dayList = await instructor.GetFromJsonAsync<OwnedSlotListBody>(
            $"/api/providers/me/slots?mode=Home&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}",
            Json);
        var slot = dayList!.Slots.Single(s => s.Start == "08:00");
        Assert.Equal(2, dayList.Slots.Count);

        var blocked = await PostAsync<OwnedSlotBody>(instructor, $"/api/providers/me/slots/{slot.Id}/block");
        Assert.Equal(slot.Id, blocked.Id);
        Assert.True(blocked.IsBlocked);
        Assert.Equal("08:00", blocked.Start);
        Assert.Equal("09:00", blocked.End);

        var again = await PostAsync<OwnedSlotBody>(instructor, $"/api/providers/me/slots/{slot.Id}/block");
        Assert.True(again.IsBlocked);

        var hidden = await instructor.GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}",
            Json);
        Assert.DoesNotContain(hidden!.Slots, s => s.Id == slot.Id);
        Assert.Single(hidden.Slots);

        var stillMine = await instructor.GetFromJsonAsync<OwnedSlotListBody>(
            $"/api/providers/me/slots?mode=Home&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}",
            Json);
        var listed = Assert.Single(stillMine!.Slots, s => s.Id == slot.Id);
        Assert.True(listed.IsBlocked);

        var online = await instructor.GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Online&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}",
            Json);
        Assert.Equal(2, online!.Slots.Count);

        await SetBlockedAsync(slot.Id, false);
    }

    [Fact]
    public async Task Block_refuses_an_occupying_booking_and_a_past_slot()
    {
        var instructor = await InstructorClientAsync();
        var slot = await FirstHomeSlotAsync(instructor, "07:00");
        var bookingId = await InsertBookingAsync(slot.Id, BookingStatus.PendingAccept);

        foreach (var status in Enum.GetValues<BookingStatus>().Where(BookingRules.OccupiesSlot))
        {
            await SetBookingStatusAsync(bookingId, status);
            var conflict = await instructor.PostAsync($"/api/providers/me/slots/{slot.Id}/block", null);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Contains("booking", (await conflict.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(await IsBlockedAsync(slot.Id));
        }

        await SetBookingStatusAsync(bookingId, BookingStatus.Cancelled);
        var freed = await PostAsync<OwnedSlotBody>(instructor, $"/api/providers/me/slots/{slot.Id}/block");
        Assert.True(freed.IsBlocked);
        await SetBlockedAsync(slot.Id, false);

        var pastId = await InsertSlotAsync(MumbaiClock.Today().AddDays(-1), new TimeOnly(7, 0), new TimeOnly(8, 0));
        var past = await instructor.PostAsync($"/api/providers/me/slots/{pastId}/block", null);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains("past", (await past.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await IsBlockedAsync(pastId));

        await DeleteBookingAsync(bookingId);
        await DeleteSlotAsync(pastId);
    }

    [Fact]
    public async Task Block_and_list_refuse_another_instructor_and_a_customer()
    {
        var owner = await InstructorClientAsync();
        var slot = await FirstHomeSlotAsync(owner, "08:00");

        var other = _factory.CreateClient();
        var (token, _) = await SignUpAsync(other, "Priya Nair", "Female");
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var areas = await other.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var registered = await other.PostAsJsonAsync("/api/providers/register", new
        {
            displayName = "Priya Nair",
            age = 29,
            areaId = areas!.Single(a => a.Name == "Bandra").Id,
            offersHome = true,
            homeRate = 700
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var body = await registered.Content.ReadFromJsonAsync<RegisterBody>(Json);
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);

        var wrong = await other.PostAsync($"/api/providers/me/slots/{slot.Id}/block", null);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Contains("another instructor", (await wrong.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await IsBlockedAsync(slot.Id));

        var missing = await other.PostAsync($"/api/providers/me/slots/{Guid.NewGuid()}/block", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var theirs = await other.GetFromJsonAsync<OwnedSlotListBody>("/api/providers/me/slots?mode=Home", Json);
        Assert.Empty(theirs!.Slots);
        Assert.DoesNotContain(theirs.Slots, s => s.Id == slot.Id);

        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Rahul Sharma", "Male");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var list = await customer.GetAsync("/api/providers/me/slots?mode=Home");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        var block = await customer.PostAsync($"/api/providers/me/slots/{slot.Id}/block", null);
        Assert.Equal(HttpStatusCode.NotFound, block.StatusCode);
        Assert.False(await IsBlockedAsync(slot.Id));
    }

    private async Task<HttpClient> InstructorClientAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AnanyaPhone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AnanyaPhone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<OwnedSlotBody> FirstHomeSlotAsync(HttpClient instructor, string start)
    {
        var mine = await instructor.GetFromJsonAsync<OwnedSlotListBody>("/api/providers/me/slots?mode=Home", Json);
        return mine!.Slots.Where(s => s.Start == start).OrderBy(s => s.Date).First();
    }

    private async Task<Guid> InsertBookingAsync(Guid slotId, BookingStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var customer = new User
        {
            Id = Guid.NewGuid(),
            Phone = NewPhone(),
            Name = "Slot Hold",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var bookingId = Guid.NewGuid();
        db.Users.Add(customer);
        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            CustomerId = customer.Id,
            ProviderId = SeedIds.AnanyaProviderId,
            ServiceId = SeedIds.AnanyaServiceId,
            SlotId = slotId,
            Mode = SessionMode.Home,
            Status = status,
            Amount = 899m,
            HomeAddress = "Bandra West",
            Landmark = "Near the station",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return bookingId;
    }

    private async Task<Guid> InsertSlotAsync(DateOnly date, TimeOnly start, TimeOnly end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var id = Guid.NewGuid();
        db.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = id,
            ProviderId = SeedIds.AnanyaProviderId,
            Mode = SessionMode.Home,
            Date = date,
            StartTime = start,
            EndTime = end,
            IsBlocked = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task SetBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task SetBlockedAsync(Guid slotId, bool blocked)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = await db.AvailabilitySlots.SingleAsync(s => s.Id == slotId);
        slot.IsBlocked = blocked;
        await db.SaveChangesAsync();
    }

    private async Task<bool> IsBlockedAsync(Guid slotId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.AvailabilitySlots.Where(s => s.Id == slotId).Select(s => s.IsBlocked).SingleAsync();
    }

    private async Task DeleteBookingAsync(Guid bookingId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is not null)
            db.Bookings.Remove(booking);
        await db.SaveChangesAsync();
    }

    private async Task DeleteSlotAsync(Guid slotId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = await db.AvailabilitySlots.FindAsync(slotId);
        if (slot is not null)
            db.AvailabilitySlots.Remove(slot);
        await db.SaveChangesAsync();
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name, string gender)
    {
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object? body = null)
    {
        var response = body is null
            ? await client.PostAsync(url, null)
            : await client.PostAsJsonAsync(url, body);
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
    private sealed record RegisterBody(string Token);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
    private sealed record OwnedSlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End, bool IsBlocked);
    private sealed record OwnedSlotListBody(string Mode, DateOnly From, DateOnly To, List<OwnedSlotBody> Slots);
}

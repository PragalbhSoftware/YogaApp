using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class BookingCustomerChangeTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public BookingCustomerChangeTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cancel_pending_accept_refunds_and_frees_the_slot()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Meera Kulkarni", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slot = await FirstOpenSlotAsync(customer, "Home");
        var booked = await BookHomeAsync(customer, slot.Id, "pay_cancel_pending");
        Assert.Equal("PendingAccept", booked.Status);

        var cancelled = await CancelAsync(customer, booked.Id);
        Assert.Equal(booked.Id, cancelled.Id);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Refunded", cancelled.PaymentStatus);
        Assert.Equal(slot.Id, cancelled.SlotId);
        Assert.Equal(899m, cancelled.Amount);

        var refund = Assert.Single(RefundsFor(booked.GatewayPaymentId!));
        Assert.Equal(89900, refund.AmountPaise);
        Assert.Equal(booked.Id.ToString("N"), refund.Receipt);
        Assert.Equal(BookingStatus.Cancelled, await BookingStatusAsync(booked.Id));
        Assert.Equal(PaymentStatus.Refunded, await PaymentStatusAsync(booked.Id));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));
        Assert.Equal(0, await OccupyingCountAsync(slot.Id));

        var listed = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        Assert.Contains(listed!.Slots, s => s.Id == slot.Id);

        var again = await customer.PostAsync($"/api/bookings/{booked.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("pending", (await again.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(RefundsFor(booked.GatewayPaymentId!));

        var next = _factory.CreateClient();
        var (nextToken, _) = await SignUpAsync(next, "Leela Shah", "Female");
        next.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nextToken);
        var rebooked = await BookHomeAsync(next, slot.Id, "pay_cancel_pending_rebook");
        Assert.Equal("PendingAccept", rebooked.Status);
        Assert.NotEqual(booked.Id, rebooked.Id);
        Assert.Equal(1, await OccupyingCountAsync(slot.Id));
    }

    [Fact]
    public async Task Cancel_upcoming_refunds_and_frees_the_slot()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Arjun Mehta", "Male");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slot = await FirstOpenSlotAsync(customer, "Studio");
        var booked = await BookAsync(customer, slot.Id, "pay_cancel_upcoming", homeAddress: null, landmark: null);
        Assert.Equal(749m, booked.Amount);

        var instructor = await InstructorClientAsync();
        var accepted = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
        Assert.Equal("Upcoming", accepted.Status);
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

        var cancelled = await CancelAsync(customer, booked.Id);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Refunded", cancelled.PaymentStatus);
        Assert.Equal(BookingStatus.Cancelled, await BookingStatusAsync(booked.Id));
        Assert.Equal(PaymentStatus.Refunded, await PaymentStatusAsync(booked.Id));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));
        Assert.Equal(0, await OccupyingCountAsync(slot.Id));

        var refund = Assert.Single(RefundsFor(booked.GatewayPaymentId!));
        Assert.Equal(74900, refund.AmountPaise);
        Assert.Equal(booked.Id.ToString("N"), refund.Receipt);

        var listed = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Studio", Json);
        Assert.Contains(listed!.Slots, s => s.Id == slot.Id);
    }

    [Fact]
    public async Task Reschedule_keeps_one_paid_booking_on_an_open_slot()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Naina Bose", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var original = await FirstOpenSlotAsync(customer, "Home");
        var booked = await BookHomeAsync(customer, original.Id, "pay_reschedule_ok");
        var instructor = await InstructorClientAsync();
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });

        var open = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        var target = open!.Slots.First(s => s.Id != original.Id);

        var moved = await RescheduleAsync(customer, booked.Id, target.Id);
        Assert.Equal(booked.Id, moved.Id);
        Assert.Equal("Upcoming", moved.Status);
        Assert.Equal("Paid", moved.PaymentStatus);
        Assert.Equal(target.Id, moved.SlotId);
        Assert.Equal("Home", moved.Mode);
        Assert.Equal(SeedIds.AnanyaProviderId, moved.ProviderId);
        Assert.Equal(899m, moved.Amount);
        Assert.Equal("14th Road, Bandra West", moved.HomeAddress);
        Assert.Equal(booked.GatewayPaymentId, moved.GatewayPaymentId);
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(booked.Id));
        Assert.Equal(target.Id, await BookingSlotAsync(booked.Id));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));
        Assert.Equal(0, await OccupyingCountAsync(original.Id));
        Assert.Equal(1, await OccupyingCountAsync(target.Id));
        Assert.Equal(1, await CustomerBookingCountAsync(booked.Id));

        var listed = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        Assert.Contains(listed!.Slots, s => s.Id == original.Id);
        Assert.DoesNotContain(listed.Slots, s => s.Id == target.Id);

        var mine = await customer.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        Assert.Contains(mine!, b => b.Id == booked.Id && b.SlotId == target.Id && b.Status == "Upcoming" && b.PaymentStatus == "Paid");

        var next = _factory.CreateClient();
        var (nextToken, _) = await SignUpAsync(next, "Kabir Das", "Male");
        next.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nextToken);
        var rebooked = await BookHomeAsync(next, original.Id, "pay_reschedule_old_slot");
        Assert.Equal("PendingAccept", rebooked.Status);
        Assert.NotEqual(booked.Id, rebooked.Id);
    }

    [Fact]
    public async Task Illegal_cancel_and_reschedule_states_are_rejected()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Riya Sen", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var instructor = await InstructorClientAsync();

        var pendingSlot = await FirstOpenSlotAsync(customer, "Online");
        var pending = await BookAsync(customer, pendingSlot.Id, "pay_change_pending", homeAddress: null, landmark: null);
        var otherOnline = await AnotherOpenSlotAsync(customer, "Online", pendingSlot.Id);

        var tooSoon = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = otherOnline.Id });
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);
        Assert.Contains("upcoming", (await tooSoon.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(pendingSlot.Id, await BookingSlotAsync(pending.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(pending.Id));

        await PostAsync<BookingBody>(instructor, $"/api/bookings/{pending.Id}/accept", new { });

        var sameSlot = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = pendingSlot.Id });
        Assert.Equal(HttpStatusCode.BadRequest, sameSlot.StatusCode);
        Assert.Contains("different", (await sameSlot.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var home = await FirstOpenSlotAsync(customer, "Home");
        var otherMode = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = home.Id });
        Assert.Equal(HttpStatusCode.BadRequest, otherMode.StatusCode);
        Assert.Contains("mode", (await otherMode.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var otherInstructorSlot = await OtherInstructorHomeSlotAsync();
        var otherInstructor = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = otherInstructorSlot });
        Assert.Equal(HttpStatusCode.BadRequest, otherInstructor.StatusCode);
        Assert.Contains("instructor", (await otherInstructor.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        await SetBlockedAsync(otherOnline.Id, true);
        var blocked = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = otherOnline.Id });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("blocked", (await blocked.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        await SetBlockedAsync(otherOnline.Id, false);

        var ended = await AnotherOpenSlotAsync(customer, "Online", pendingSlot.Id);
        await ShiftSlotToYesterdayAsync(ended.Id);
        var endedResponse = await customer.PostAsJsonAsync($"/api/bookings/{pending.Id}/reschedule", new { slotId = ended.Id });
        Assert.Equal(HttpStatusCode.Conflict, endedResponse.StatusCode);
        Assert.Contains("ended", (await endedResponse.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(pendingSlot.Id, await BookingSlotAsync(pending.Id));
        Assert.Equal(BookingStatus.Upcoming, await BookingStatusAsync(pending.Id));
        Assert.Empty(RefundsFor(pending.GatewayPaymentId!));

        await PostAsync<BookingBody>(instructor, $"/api/bookings/{pending.Id}/complete", new { });
        var cancelCompleted = await customer.PostAsync($"/api/bookings/{pending.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelCompleted.StatusCode);
        Assert.Contains("pending or upcoming", (await cancelCompleted.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.Completed, await BookingStatusAsync(pending.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(pending.Id));
        Assert.Empty(RefundsFor(pending.GatewayPaymentId!));
        Assert.Equal(1, await PayoutCountAsync(pending.Id));

        var declinedSlot = await FirstOpenSlotAsync(customer, "Studio");
        var declined = await BookAsync(customer, declinedSlot.Id, "pay_change_declined", homeAddress: null, landmark: null);
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{declined.Id}/decline", new { });
        var cancelDeclined = await customer.PostAsync($"/api/bookings/{declined.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelDeclined.StatusCode);
        Assert.Equal(BookingStatus.Declined, await BookingStatusAsync(declined.Id));
        Assert.Single(RefundsFor(declined.GatewayPaymentId!));

        var startedSlot = await FirstOpenSlotAsync(customer, "Home");
        var started = await BookHomeAsync(customer, startedSlot.Id, "pay_change_started");
        await ShiftSlotToYesterdayAsync(startedSlot.Id);
        var cancelStarted = await customer.PostAsync($"/api/bookings/{started.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelStarted.StatusCode);
        Assert.Contains("started", (await cancelStarted.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.PendingAccept, await BookingStatusAsync(started.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(started.Id));
        Assert.Empty(RefundsFor(started.GatewayPaymentId!));
        Assert.Equal(1, await OccupyingCountAsync(startedSlot.Id));
    }

    [Fact]
    public async Task Wrong_user_cannot_cancel_or_reschedule()
    {
        var customer = _factory.CreateClient();
        var (token, _) = await SignUpAsync(customer, "Asha Iyer", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var stranger = _factory.CreateClient();
        var (strangerToken, _) = await SignUpAsync(stranger, "Dev Patel", "Male");
        stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);

        var slot = await FirstOpenSlotAsync(customer, "Online");
        var booked = await BookAsync(customer, slot.Id, "pay_change_auth", homeAddress: null, landmark: null);
        var instructor = await InstructorClientAsync();
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
        var target = await AnotherOpenSlotAsync(customer, "Online", slot.Id);
        var admin = await AdminClientAsync();

        foreach (var caller in new[] { stranger, instructor, admin })
        {
            await AssertForbiddenAsync(caller, $"/api/bookings/{booked.Id}/cancel", null);
            await AssertForbiddenAsync(caller, $"/api/bookings/{booked.Id}/reschedule", new { slotId = target.Id });
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsync($"/api/bookings/{booked.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync($"/api/bookings/{booked.Id}/reschedule", new { slotId = target.Id })).StatusCode);
        Assert.Equal(BookingStatus.Upcoming, await BookingStatusAsync(booked.Id));
        Assert.Equal(slot.Id, await BookingSlotAsync(booked.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(booked.Id));
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

        var nullBody = new HttpRequestMessage(HttpMethod.Post, $"/api/bookings/{booked.Id}/reschedule")
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };
        var nullResponse = await customer.SendAsync(nullBody);
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);
        Assert.Contains("Invalid request", (await nullResponse.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        var missingSlot = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reschedule", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missingSlot.StatusCode);
        Assert.Contains("Slot is required", (await missingSlot.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsync($"/api/bookings/{Guid.NewGuid()}/cancel", null)).StatusCode);
        var unknownSlot = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reschedule", new { slotId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, unknownSlot.StatusCode);
        Assert.Contains("slot", (await unknownSlot.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(slot.Id, await BookingSlotAsync(booked.Id));
    }

    [Fact]
    public async Task Reschedule_rejects_a_slot_that_is_already_taken()
    {
        var first = _factory.CreateClient();
        var (firstToken, _) = await SignUpAsync(first, "Tara Menon", "Female");
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var second = _factory.CreateClient();
        var (secondToken, _) = await SignUpAsync(second, "Omkar Rao", "Male");
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);

        var slot = await FirstOpenSlotAsync(first, "Home");
        var other = await AnotherOpenSlotAsync(first, "Home", slot.Id);
        var booked = await BookHomeAsync(first, slot.Id, "pay_reschedule_conflict");
        var occupying = await BookHomeAsync(second, other.Id, "pay_reschedule_taken");
        var instructor = await InstructorClientAsync();
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });

        var conflict = await first.PostAsJsonAsync($"/api/bookings/{booked.Id}/reschedule", new { slotId = other.Id });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("slot", (await conflict.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(BookingStatus.Upcoming, await BookingStatusAsync(booked.Id));
        Assert.Equal(slot.Id, await BookingSlotAsync(booked.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(booked.Id));
        Assert.Equal(BookingStatus.PendingAccept, await BookingStatusAsync(occupying.Id));
        Assert.Equal(other.Id, await BookingSlotAsync(occupying.Id));
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));
        Assert.Empty(RefundsFor(occupying.GatewayPaymentId!));
        Assert.Equal(1, await OccupyingCountAsync(slot.Id));
        Assert.Equal(1, await OccupyingCountAsync(other.Id));
    }

    private async Task AssertForbiddenAsync(HttpClient client, string path, object? body)
    {
        var response = body is null ? await client.PostAsync(path, null) : await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error));
    }

    private async Task<BookingBody> CancelAsync(HttpClient client, Guid bookingId) =>
        await PostAsync<BookingBody>(client, $"/api/bookings/{bookingId}/cancel", new { });

    private async Task<BookingBody> RescheduleAsync(HttpClient client, Guid bookingId, Guid slotId) =>
        await PostAsync<BookingBody>(client, $"/api/bookings/{bookingId}/reschedule", new { slotId });

    private async Task<BookingBody> BookHomeAsync(HttpClient client, Guid slotId, string paymentId) =>
        await BookAsync(client, slotId, paymentId, "14th Road, Bandra West", "Near the station");

    private async Task<BookingBody> BookAsync(HttpClient client, Guid slotId, string paymentId, string? homeAddress, string? landmark)
    {
        var order = await PostAsync<CheckoutBody>(client, "/api/bookings/orders", new { slotId, homeAddress, landmark });
        return await PostAsync<BookingBody>(client, "/api/bookings/confirm", new
        {
            orderId = order.OrderId,
            paymentId,
            signature = SignPayment(order.OrderId, paymentId)
        });
    }

    private async Task<SlotBody> AnotherOpenSlotAsync(HttpClient client, string mode, Guid exceptId)
    {
        var list = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode={mode}", Json);
        var slot = list!.Slots.FirstOrDefault(s => s.Id != exceptId);
        Assert.NotNull(slot);
        return slot!;
    }

    private async Task<Guid> OtherInstructorHomeSlotAsync()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Vikram Nair", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var registered = await PostAsync<RegisterBody>(client, "/api/providers/register", new
        {
            displayName = "Vikram Nair",
            age = 34,
            areaId = SeedIds.AreaBandra,
            offersHome = true,
            homeRate = 700m
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        var day = MumbaiClock.Today().AddDays(2);
        var created = await PostAsync<List<SlotBody>>(client, "/api/providers/me/slots", new
        {
            mode = "Home",
            slots = new[] { new { date = day, start = "16:00", end = "17:00" } }
        });
        return Assert.Single(created).Id;
    }

    private async Task<HttpClient> InstructorClientAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = "9876543210", isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = "9876543210", code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AdminPhone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AdminPhone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<SlotBody> FirstOpenSlotAsync(HttpClient client, string mode)
    {
        var list = await client.GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode={mode}", Json);
        var slot = list!.Slots.OrderByDescending(s => s.Date).ThenByDescending(s => s.Start).FirstOrDefault();
        Assert.NotNull(slot);
        return slot!;
    }

    private async Task<(string Token, string Phone)> SignUpAsync(HttpClient client, string name, string gender)
    {
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        return (auth.Token, phone);
    }

    private async Task ShiftSlotToYesterdayAsync(Guid slotId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = await db.AvailabilitySlots.SingleAsync(s => s.Id == slotId);
        slot.Date = MumbaiClock.Today().AddDays(-1);
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

    private IReadOnlyList<RecordedRefund> RefundsFor(string paymentId) =>
        Assert.IsType<FakeRazorpayClient>(_factory.Services.GetRequiredService<IRazorpayClient>())
            .Refunds.Where(r => r.PaymentId == paymentId).ToArray();

    private async Task<int> PayoutCountAsync(Guid bookingId) =>
        await CountAsync(db => db.PayoutsPending.CountAsync(p => p.BookingId == bookingId));

    private async Task<BookingStatus> BookingStatusAsync(Guid bookingId) =>
        await CountAsync(async db => (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status);

    private async Task<Guid> BookingSlotAsync(Guid bookingId) =>
        await CountAsync(async db => (await db.Bookings.SingleAsync(b => b.Id == bookingId)).SlotId);

    private async Task<PaymentStatus> PaymentStatusAsync(Guid bookingId) =>
        await CountAsync(async db => (await db.Payments.SingleAsync(p => p.BookingId == bookingId)).Status);

    private async Task<int> OccupyingCountAsync(Guid slotId) =>
        await CountAsync(async db =>
        {
            var rows = await db.Bookings.Where(b => b.SlotId == slotId).Select(b => b.Status).ToListAsync();
            return rows.Count(BookingRules.OccupiesSlot);
        });

    private async Task<int> CustomerBookingCountAsync(Guid bookingId) =>
        await CountAsync(async db =>
        {
            var customerId = await db.Bookings.Where(b => b.Id == bookingId).Select(b => b.CustomerId).SingleAsync();
            return await db.Bookings.CountAsync(b => b.CustomerId == customerId);
        });

    private async Task<T> CountAsync<T>(Func<YogaDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await query(db);
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

    private static string SignPayment(string orderId, string paymentId) => Sign(KeySecret, $"{orderId}|{paymentId}");

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string NewPhone() => "+9196" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record RegisterBody(string Token);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
    private sealed record CheckoutBody(Guid CheckoutId, string KeyId, string OrderId, long AmountPaise, decimal Amount, string Currency);
    private sealed record BookingBody(
        Guid Id,
        Guid ProviderId,
        string ProviderName,
        Guid SlotId,
        string Mode,
        string Status,
        decimal Amount,
        string? HomeAddress,
        string PaymentStatus,
        string? GatewayPaymentId);
}

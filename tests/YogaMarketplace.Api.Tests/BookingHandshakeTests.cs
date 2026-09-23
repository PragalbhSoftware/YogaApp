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

public class BookingHandshakeTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public BookingHandshakeTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Accept_then_complete_unlocks_one_customer_review()
    {
        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Meera Kulkarni", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var slot = await FirstOpenSlotAsync(customer, "Home");
        var booked = await BookHomeAsync(customer, slot.Id, "pay_accept_1");
        Assert.Equal("PendingAccept", booked.Status);
        Assert.Equal("Paid", booked.PaymentStatus);

        var anon = await _factory.CreateClient().GetAsync("/api/bookings/instructor");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var customerInbox = await customer.GetAsync("/api/bookings/instructor?status=PendingAccept");
        Assert.Equal(HttpStatusCode.Forbidden, customerInbox.StatusCode);

        var instructor = await InstructorClientAsync();
        var customerMine = await instructor.GetAsync("/api/bookings/me");
        Assert.Equal(HttpStatusCode.Forbidden, customerMine.StatusCode);

        var pending = await instructor.GetFromJsonAsync<List<BookingBody>>("/api/bookings/instructor?status=PendingAccept", Json);
        Assert.Contains(pending!, b => b.Id == booked.Id && b.Status == "PendingAccept");

        var upcomingFilter = await instructor.GetFromJsonAsync<List<BookingBody>>("/api/bookings/instructor?status=Upcoming", Json);
        Assert.DoesNotContain(upcomingFilter!, b => b.Id == booked.Id);

        var accepted = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
        Assert.Equal("Upcoming", accepted.Status);
        Assert.Equal("Paid", accepted.PaymentStatus);
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));

        var hidden = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        Assert.DoesNotContain(hidden!.Slots, s => s.Id == slot.Id);

        var mine = await customer.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        Assert.Contains(mine!, b => b.Id == booked.Id && b.Status == "Upcoming");

        var earlyReview = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 5, comment = "Too soon" });
        Assert.Equal(HttpStatusCode.BadRequest, earlyReview.StatusCode);
        Assert.Contains("completed", (await earlyReview.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await ReviewCountAsync(booked.Id));

        var completed = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/complete", new { });
        Assert.Equal("Completed", completed.Status);
        Assert.Equal("Paid", completed.PaymentStatus);
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

        var payout = await LoadPayoutAsync(booked.Id);
        Assert.Equal(PayoutStatus.Pending, payout.Status);
        Assert.Equal(15m, payout.FeePercent);
        Assert.Equal(899m, payout.GrossAmount);
        Assert.Equal(134.85m, payout.FeeAmount);
        Assert.Equal(764.15m, payout.NetAmount);

        var stillHidden = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        Assert.DoesNotContain(stillHidden!.Slots, s => s.Id == slot.Id);

        var done = await instructor.GetFromJsonAsync<List<BookingBody>>("/api/bookings/instructor?status=completed", Json);
        Assert.Contains(done!, b => b.Id == booked.Id && b.Status == "Completed");

        var review = await PostReviewAsync(customer, booked.Id, 5, "  Calm and clear  ");
        Assert.Equal(booked.Id, review.BookingId);
        Assert.Equal(SeedIds.AnanyaProviderId, review.ProviderId);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Calm and clear", review.Comment);

        var again = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 4, comment = "Second thought" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already", (await again.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ReviewCountAsync(booked.Id));
        Assert.Equal(5, (await LoadReviewAsync(booked.Id)).Rating);
    }

    [Fact]
    public async Task Decline_refunds_frees_the_slot_and_does_not_create_a_payout()
    {
        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Arjun Mehta", "Male");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var slot = await FirstOpenSlotAsync(customer, "Studio");
        var booked = await BookAsync(customer, slot.Id, "pay_decline_1", homeAddress: null, landmark: null);
        Assert.Equal(749m, booked.Amount);

        var instructor = await InstructorClientAsync();
        var declined = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/decline", new { });
        Assert.Equal("Declined", declined.Status);
        Assert.Equal("Refunded", declined.PaymentStatus);

        var refund = Assert.Single(RefundsFor(booked.GatewayPaymentId!));
        Assert.Equal(74900, refund.AmountPaise);
        Assert.Equal(booked.Id.ToString("N"), refund.Receipt);
        Assert.Equal(0, await PayoutCountAsync(booked.Id));
        Assert.Equal(PaymentStatus.Refunded, await PaymentStatusAsync(booked.Id));
        Assert.Equal(BookingStatus.Declined, await BookingStatusAsync(booked.Id));

        var listed = await customer.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Studio", Json);
        Assert.Contains(listed!.Slots, s => s.Id == slot.Id);

        var next = _factory.CreateClient();
        var (nextToken, _) = await SignUpAsync(next, "Leela Shah", "Female");
        next.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nextToken);
        var rebooked = await BookAsync(next, slot.Id, "pay_decline_rebook", homeAddress: null, landmark: null);
        Assert.Equal("PendingAccept", rebooked.Status);
        Assert.NotEqual(booked.Id, rebooked.Id);
        Assert.Equal(1, await OccupyingCountAsync(slot.Id));

        var secondDecline = await instructor.PostAsync($"/api/bookings/{booked.Id}/decline", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondDecline.StatusCode);
        Assert.Single(RefundsFor(booked.GatewayPaymentId!));

        var review = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
        Assert.Equal(0, await ReviewCountAsync(booked.Id));
    }

    [Fact]
    public async Task Illegal_transitions_and_other_accounts_are_rejected()
    {
        var customer = _factory.CreateClient();
        var (customerToken, _) = await SignUpAsync(customer, "Naina Bose", "Female");
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var stranger = _factory.CreateClient();
        var (strangerToken, _) = await SignUpAsync(stranger, "Kabir Das", "Male");
        stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);

        var slot = await FirstOpenSlotAsync(customer, "Online");
        var booked = await BookAsync(customer, slot.Id, "pay_illegal_1", homeAddress: null, landmark: null);
        var instructor = await InstructorClientAsync();
        var other = await OtherInstructorClientAsync();

        var unknown = await instructor.GetAsync("/api/bookings/instructor?status=NotAStatus");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("status", (await unknown.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var otherInbox = await other.GetFromJsonAsync<List<BookingBody>>("/api/bookings/instructor", Json);
        Assert.DoesNotContain(otherInbox!, b => b.Id == booked.Id);

        await AssertForbiddenAsync(customer, $"/api/bookings/{booked.Id}/accept");
        await AssertForbiddenAsync(other, $"/api/bookings/{booked.Id}/accept");
        await AssertForbiddenAsync(other, $"/api/bookings/{booked.Id}/decline");
        await AssertForbiddenAsync(other, $"/api/bookings/{booked.Id}/complete");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsync($"/api/bookings/{booked.Id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await instructor.PostAsync($"/api/bookings/{Guid.NewGuid()}/accept", null)).StatusCode);
        Assert.Equal(BookingStatus.PendingAccept, await BookingStatusAsync(booked.Id));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

        var tooEarly = await instructor.PostAsync($"/api/bookings/{booked.Id}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Contains("upcoming", (await tooEarly.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.PendingAccept, await BookingStatusAsync(booked.Id));
        Assert.Equal(0, await PayoutCountAsync(booked.Id));

        var accepted = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
        Assert.Equal("Upcoming", accepted.Status);

        var acceptAgain = await instructor.PostAsync($"/api/bookings/{booked.Id}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, acceptAgain.StatusCode);

        var declineLate = await instructor.PostAsync($"/api/bookings/{booked.Id}/decline", null);
        Assert.Equal(HttpStatusCode.BadRequest, declineLate.StatusCode);
        Assert.Contains("pending", (await declineLate.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.Upcoming, await BookingStatusAsync(booked.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(booked.Id));
        Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

        var strangerReview = await stranger.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 5, comment = "Not mine" });
        Assert.Equal(HttpStatusCode.Forbidden, strangerReview.StatusCode);
        var instructorReview = await instructor.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, instructorReview.StatusCode);
        Assert.Equal(0, await ReviewCountAsync(booked.Id));

        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/complete", new { });

        var completeAgain = await instructor.PostAsync($"/api/bookings/{booked.Id}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, completeAgain.StatusCode);
        Assert.Equal(1, await PayoutCountAsync(booked.Id));

        var nullBody = new HttpRequestMessage(HttpMethod.Post, $"/api/bookings/{booked.Id}/reviews")
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };
        var nullReview = await customer.SendAsync(nullBody);
        Assert.Equal(HttpStatusCode.BadRequest, nullReview.StatusCode);

        foreach (var rating in new[] { 0, 6 })
        {
            var badRating = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating, comment = "No" });
            Assert.Equal(HttpStatusCode.BadRequest, badRating.StatusCode);
            Assert.Contains("1 and 5", (await badRating.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);
        }

        var longComment = await customer.PostAsJsonAsync($"/api/bookings/{booked.Id}/reviews", new { rating = 4, comment = new string('a', 1001) });
        Assert.Equal(HttpStatusCode.BadRequest, longComment.StatusCode);
        Assert.Contains("1000", (await longComment.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);
        Assert.Equal(0, await ReviewCountAsync(booked.Id));

        var blank = await PostReviewAsync(customer, booked.Id, 4, "   ");
        Assert.Null(blank.Comment);
        Assert.Equal(4, blank.Rating);
        Assert.Equal(1, await PayoutCountAsync(booked.Id));
        Assert.Equal(PaymentStatus.Paid, await PaymentStatusAsync(booked.Id));
    }

    private async Task AssertForbiddenAsync(HttpClient client, string path)
    {
        var response = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error));
    }

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

    private async Task<ReviewBody> PostReviewAsync(HttpClient client, Guid bookingId, int rating, string? comment)
    {
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/reviews", new { rating, comment });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith($"/api/bookings/{bookingId}/reviews/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var parsed = JsonSerializer.Deserialize<ReviewBody>(payload, Json);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private async Task<HttpClient> InstructorClientAsync()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = "9876543210", isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = "9876543210", code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<HttpClient> OtherInstructorClientAsync()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Vikram Nair", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var registered = await PostAsync<RegisterBody>(client, "/api/providers/register", new
        {
            displayName = "Vikram Nair",
            age = 34,
            areaId = SeedIds.AreaBandra,
            offersOnline = true,
            onlineRate = 500m,
            googleMeetLink = "https://meet.google.com/other-yoga-link"
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
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

    private IReadOnlyList<RecordedRefund> RefundsFor(string paymentId) =>
        Assert.IsType<FakeRazorpayClient>(_factory.Services.GetRequiredService<IRazorpayClient>())
            .Refunds.Where(r => r.PaymentId == paymentId).ToArray();

    private async Task<int> PayoutCountAsync(Guid bookingId) =>
        await CountAsync(db => db.PayoutsPending.CountAsync(p => p.BookingId == bookingId));

    private async Task<PayoutPending> LoadPayoutAsync(Guid bookingId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.PayoutsPending.SingleAsync(p => p.BookingId == bookingId);
    }

    private async Task<Review> LoadReviewAsync(Guid bookingId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return await db.Reviews.SingleAsync(r => r.BookingId == bookingId);
    }

    private async Task<int> ReviewCountAsync(Guid bookingId) =>
        await CountAsync(db => db.Reviews.CountAsync(r => r.BookingId == bookingId));

    private async Task<BookingStatus> BookingStatusAsync(Guid bookingId) =>
        await CountAsync(async db => (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status);

    private async Task<PaymentStatus> PaymentStatusAsync(Guid bookingId) =>
        await CountAsync(async db => (await db.Payments.SingleAsync(p => p.BookingId == bookingId)).Status);

    private async Task<int> OccupyingCountAsync(Guid slotId) =>
        await CountAsync(async db =>
        {
            var rows = await db.Bookings.Where(b => b.SlotId == slotId).Select(b => b.Status).ToListAsync();
            return rows.Count(BookingRules.OccupiesSlot);
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
        string PaymentStatus,
        string? GatewayPaymentId);
    private sealed record ReviewBody(Guid Id, Guid BookingId, Guid ProviderId, int Rating, string? Comment);
}

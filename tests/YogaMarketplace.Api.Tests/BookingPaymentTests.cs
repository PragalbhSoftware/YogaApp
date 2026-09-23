using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class BookingPaymentTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";
    private const string WebhookSecret = "dev-only-not-a-live-webhook-secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public BookingPaymentTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Pay_at_book_confirms_to_pending_accept_and_is_idempotent()
    {
        var client = _factory.CreateClient();
        var anon = await client.PostAsJsonAsync("/api/bookings/orders", new { slotId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var (token, _) = await SignUpAsync(client, "Rahul Sharma", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slot = await FirstOpenSlotAsync(client, "Home");
        var order = await CreateOrderAsync(client, slot.Id, "  14th Road, Bandra West  ", "Near the station");
        Assert.Equal(89900, order.AmountPaise);
        Assert.Equal(899m, order.Amount);
        Assert.Equal("INR", order.Currency);
        Assert.StartsWith("order_fake_", order.OrderId, StringComparison.Ordinal);
        Assert.Equal("rzp_test_placeholder", order.KeyId);
        Assert.Equal(0, await CountBookingsAsync(slot.Id));

        var bad = await client.PostAsJsonAsync("/api/bookings/confirm", new
        {
            orderId = order.OrderId,
            paymentId = "pay_home_1",
            signature = "not-a-valid-signature"
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("signature", (await bad.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountBookingsAsync(slot.Id));

        var paymentId = "pay_home_1";
        var first = await ConfirmAsync(client, order.OrderId, paymentId);
        var second = await ConfirmAsync(client, order.OrderId, paymentId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("PendingAccept", first.Status);
        Assert.Equal("Paid", first.PaymentStatus);
        Assert.Equal("Home", first.Mode);
        Assert.Equal("14th Road, Bandra West", first.HomeAddress);
        Assert.Equal("Near the station", first.Landmark);
        Assert.Null(first.MeetLink);
        Assert.Equal(paymentId, first.GatewayPaymentId);
        Assert.Equal(order.OrderId, first.GatewayOrderId);
        Assert.Equal(899m, first.Amount);
        Assert.Equal(1, await CountBookingsAsync(slot.Id));
        Assert.Equal(0, await CountAsync(db => db.PayoutsPending.CountAsync()));

        var mine = await client.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        Assert.Contains(mine!, b => b.Id == first.Id && b.Status == "PendingAccept" && b.ProviderName == "Ananya Desai");

        var other = _factory.CreateClient();
        var (otherToken, _) = await SignUpAsync(other, "Meera Iyer", "Female");
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var others = await other.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        Assert.DoesNotContain(others!, b => b.Id == first.Id);

        var listed = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Home", Json);
        Assert.DoesNotContain(listed!.Slots, s => s.Id == slot.Id);

        var replay = await client.PostAsJsonAsync("/api/bookings/confirm", new
        {
            orderId = order.OrderId,
            paymentId = "pay_home_other",
            signature = SignPayment(order.OrderId, "pay_home_other")
        });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(1, await CountBookingsAsync(slot.Id));
    }

    [Fact]
    public async Task Second_payment_for_the_same_slot_is_rejected()
    {
        var firstClient = _factory.CreateClient();
        var (firstToken, _) = await SignUpAsync(firstClient, "Asha Patel", "Female");
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);

        var secondClient = _factory.CreateClient();
        var (secondToken, _) = await SignUpAsync(secondClient, "Kabir Shah", "Male");
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);

        var slot = await FirstOpenSlotAsync(firstClient, "Home");
        var firstOrder = await CreateOrderAsync(firstClient, slot.Id, "Pali Hill", "Opposite the market");
        var secondOrder = await CreateOrderAsync(secondClient, slot.Id, "Pali Hill", "Opposite the market");
        Assert.NotEqual(firstOrder.OrderId, secondOrder.OrderId);
        Assert.Equal(0, await CountBookingsAsync(slot.Id));

        var booked = await ConfirmAsync(firstClient, firstOrder.OrderId, "pay_slot_winner");
        Assert.Equal("PendingAccept", booked.Status);

        var lost = await secondClient.PostAsJsonAsync("/api/bookings/confirm", new
        {
            orderId = secondOrder.OrderId,
            paymentId = "pay_slot_loser",
            signature = SignPayment(secondOrder.OrderId, "pay_slot_loser")
        });
        Assert.Equal(HttpStatusCode.Conflict, lost.StatusCode);
        Assert.Equal(1, await CountBookingsAsync(slot.Id));

        var again = await secondClient.PostAsJsonAsync("/api/bookings/orders", new
        {
            slotId = slot.Id,
            homeAddress = "Pali Hill",
            landmark = "Opposite the market"
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Unpaid_checkout_does_not_create_a_booking()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Neel Joshi", "Other");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Home");
        var before = await CountBookingsAsync(slot.Id);

        var order = await CreateOrderAsync(client, slot.Id, "Carter Road", "Near the bandstand");

        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(1, await CountAsync(db => db.CheckoutIntents.CountAsync(c => c.GatewayOrderId == order.OrderId && c.Status == CheckoutStatus.Open)));
        var mine = await client.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        Assert.DoesNotContain(mine!, b => b.SlotId == slot.Id);
    }

    [Fact]
    public async Task Home_order_requires_address_and_landmark()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Riya Kulkarni", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Home");
        var before = await CountBookingsAsync(slot.Id);
        var checkoutsBefore = await CountAsync(db => db.CheckoutIntents.CountAsync(c => c.SlotId == slot.Id));

        var noLandmark = await client.PostAsJsonAsync("/api/bookings/orders", new
        {
            slotId = slot.Id,
            homeAddress = "14th Road, Bandra West"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noLandmark.StatusCode);
        Assert.Contains("landmark", (await noLandmark.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        var noAddress = await client.PostAsJsonAsync("/api/bookings/orders", new
        {
            slotId = slot.Id,
            landmark = "Near the station"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noAddress.StatusCode);
        Assert.Contains("address", (await noAddress.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(checkoutsBefore, await CountAsync(db => db.CheckoutIntents.CountAsync(c => c.SlotId == slot.Id)));
    }

    [Fact]
    public async Task Webhook_with_a_bad_signature_is_rejected()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Devika Rao", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);

        var body = CapturedBody(order.OrderId, "pay_bad_sig", order.AmountPaise);
        var response = await PostWebhookAsync(body, "deadbeef");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("signature", (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Open, await CheckoutStatusOfAsync(order.OrderId));
    }

    [Fact]
    public async Task Webhook_capture_books_once_and_a_failed_event_does_not()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Imran Qureshi", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);

        var failed = "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_failed\",\"order_id\":\""
            + order.OrderId + "\",\"status\":\"failed\",\"amount\":" + order.AmountPaise + ",\"currency\":\"INR\"}}}}";
        var failedResponse = await PostWebhookAsync(failed, SignWebhook(failed));
        Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
        var failedBody = await failedResponse.Content.ReadFromJsonAsync<WebhookBody>(Json);
        Assert.False(failedBody!.Booked);
        Assert.Equal(0, await CountBookingsAsync(slot.Id));

        var paymentId = "pay_studio_webhook";
        var body = CapturedBody(order.OrderId, paymentId, order.AmountPaise);
        var first = await PostWebhookAsync(body, SignWebhook(body));
        var second = await PostWebhookAsync(body, SignWebhook(body));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var booked = await first.Content.ReadFromJsonAsync<WebhookBody>(Json);
        var replay = await second.Content.ReadFromJsonAsync<WebhookBody>(Json);
        Assert.True(booked!.Booked);
        Assert.Equal(booked.BookingId, replay!.BookingId);
        Assert.Equal(1, await CountBookingsAsync(slot.Id));

        var mine = await client.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
        var booking = Assert.Single(mine!, b => b.Id == booked.BookingId);
        Assert.Equal("PendingAccept", booking.Status);
        Assert.Equal("Paid", booking.PaymentStatus);
        Assert.Equal("Studio", booking.Mode);
        Assert.Equal("Lotus Studio, Bandra West, Mumbai", booking.StudioAddress);
        Assert.Null(booking.HomeAddress);
        Assert.Equal(749m, booking.Amount);
        Assert.Equal(paymentId, booking.GatewayPaymentId);
    }

    [Fact]
    public async Task Online_booking_snapshots_the_meet_link_for_the_customer_only()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Sana Merchant", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Online");
        var created = await client.PostAsJsonAsync("/api/bookings/orders", new { slotId = slot.Id });
        var unpaidJson = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.DoesNotContain("meet.google.com", unpaidJson, StringComparison.OrdinalIgnoreCase);
        var order = JsonSerializer.Deserialize<CheckoutBody>(unpaidJson, Json);
        Assert.NotNull(order);

        var booking = await ConfirmAsync(client, order!.OrderId, "pay_online_1");
        Assert.Equal("PendingAccept", booking.Status);
        Assert.Equal("Online", booking.Mode);
        Assert.Equal(599m, booking.Amount);
        Assert.Contains("meet.google.com", booking.MeetLink, StringComparison.OrdinalIgnoreCase);
        Assert.Null(booking.HomeAddress);

        var profile = await client.GetStringAsync($"/api/providers/{SeedIds.AnanyaProviderId}");
        Assert.DoesNotContain("meet.google.com", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Instructor_cannot_open_a_customer_checkout()
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = "9876543210", isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = "9876543210", code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var slot = await FirstOpenSlotAsync(_factory.CreateClient(), "Home");

        var response = await client.PostAsJsonAsync("/api/bookings/orders", new
        {
            slotId = slot.Id,
            homeAddress = "Bandra West",
            landmark = "Near the station"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountBookingsAsync(slot.Id));
    }

    private async Task<CheckoutBody> CreateOrderAsync(HttpClient client, Guid slotId, string? address, string? landmark)
    {
        return await PostAsync<CheckoutBody>(client, "/api/bookings/orders", new
        {
            slotId,
            homeAddress = address,
            landmark
        });
    }

    private async Task<BookingBody> ConfirmAsync(HttpClient client, string orderId, string paymentId)
    {
        return await PostAsync<BookingBody>(client, "/api/bookings/confirm", new
        {
            orderId,
            paymentId,
            signature = SignPayment(orderId, paymentId)
        });
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/razorpay")
        {
            Content = new StringContent(body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Razorpay-Signature", signature);
        return await _factory.CreateClient().SendAsync(request);
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

    private async Task<int> CountBookingsAsync(Guid slotId) =>
        await CountAsync(db => db.Bookings.CountAsync(b => b.SlotId == slotId));

    private async Task<CheckoutStatus> CheckoutStatusOfAsync(string orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var checkout = await db.CheckoutIntents.SingleAsync(c => c.GatewayOrderId == orderId);
        return checkout.Status;
    }

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

    private static string SignPayment(string orderId, string paymentId) =>
        Sign(KeySecret, $"{orderId}|{paymentId}");

    private static string SignWebhook(string body) => Sign(WebhookSecret, body);

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string CapturedBody(string orderId, string paymentId, long paise) =>
        "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
        + "\",\"order_id\":\"" + orderId + "\",\"status\":\"captured\",\"amount\":" + paise + ",\"currency\":\"INR\"}}}}";

    private static string NewPhone() => "+9197" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
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
        string? Landmark,
        string? MeetLink,
        string? StudioAddress,
        string PaymentStatus,
        string? GatewayOrderId,
        string? GatewayPaymentId);
    private sealed record WebhookBody(bool Received, bool Booked, Guid? BookingId);
}

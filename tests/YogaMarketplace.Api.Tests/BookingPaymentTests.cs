using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YogaMarketplace.Api.Services;
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
    public async Task Local_confirm_captures_without_the_browser_signing()
    {
        var anon = await _factory.CreateClient().PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = "order_fake_missing" });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Diya Kapoor", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slot = await FirstOpenSlotAsync(client, "Online");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        Assert.True(order.LocalCapture);
        var booked = await PostAsync<BookingBody>(client, "/api/bookings/local-confirm", new { orderId = order.OrderId });

        Assert.Equal("PendingAccept", booked.Status);
        Assert.Equal("Paid", booked.PaymentStatus);
        Assert.Equal("Online", booked.Mode);
        Assert.Equal(order.OrderId, booked.GatewayOrderId);
        Assert.StartsWith("pay_fake_", booked.GatewayPaymentId, StringComparison.Ordinal);
        Assert.Equal(1, await CountBookingsAsync(slot.Id));
    }

    [Fact]
    public async Task Local_confirm_does_not_exist_with_the_real_gateway()
    {
        using var realGateway = _factory.WithWebHostBuilder(builder => builder.UseSetting("Razorpay:UseFakeGateway", "false"));
        var client = realGateway.CreateClient();
        var (token, _) = await SignUpAsync(client, "Meera Iyer", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = "order_any" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("\"Not found.\"", await response.Content.ReadAsStringAsync());
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
        var response = await PostWebhookAsync(body, "deadbeef", "evt_bad_sig");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("signature", (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Open, await CheckoutStatusOfAsync(order.OrderId));
        Assert.Equal(0, await CountAsync(db => db.WebhookEvents.CountAsync(e => e.EventId == "evt_bad_sig")));
    }

    [Fact]
    public async Task Webhook_before_the_client_callback_books_once_and_the_callback_returns_that_booking()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Tara Menon", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);
        var paymentId = "pay_webhook_first_" + Guid.NewGuid().ToString("N");

        var capture = CapturedBody(order.OrderId, paymentId, order.AmountPaise);
        var webhook = await PostWebhookAsync(capture, SignWebhook(capture));
        var hooked = (await webhook.Content.ReadFromJsonAsync<WebhookBody>(Json))!;
        var callback = await ConfirmAsync(client, order.OrderId, paymentId);

        Assert.True(hooked.Booked);
        Assert.Equal(hooked.BookingId, callback.Id);
        Assert.Equal(("PendingAccept", "Paid", paymentId), (callback.Status, callback.PaymentStatus, callback.GatewayPaymentId));
        Assert.Equal(before + 1, await CountBookingsAsync(slot.Id));
        Assert.Equal(1, await CountAsync(db => db.Payments.CountAsync(p => p.GatewayPaymentId == paymentId)));
        Assert.Equal(CheckoutStatus.Completed, await CheckoutStatusOfAsync(order.OrderId));
    }

    [Fact]
    public async Task Captured_webhook_with_a_wrong_amount_or_currency_is_recorded_rejected_answered_200_and_books_nothing()
    {
        var log = new ErrorLog();
        using var logged = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(log)));
        var client = logged.CreateClient();
        var (token, _) = await SignUpAsync(client, "Vikram Nair", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);
        var wrongAmount = "pay_wrong_amount_" + Guid.NewGuid().ToString("N");
        var wrongCurrency = "pay_wrong_currency_" + Guid.NewGuid().ToString("N");

        foreach (var body in new[]
        {
            CapturedBody(order.OrderId, wrongAmount, order.AmountPaise - 100),
            CapturedBody(order.OrderId, wrongCurrency, order.AmountPaise, currency: "USD")
        })
        {
            var response = await PostWebhookAsync(body, SignWebhook(body), client: client);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False((await response.Content.ReadFromJsonAsync<WebhookBody>(Json))!.Booked);
        }

        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Open, await CheckoutStatusOfAsync(order.OrderId));
        var events = await CountAsync(db => db.WebhookEvents.AsNoTracking()
            .Where(e => e.GatewayPaymentId == wrongAmount || e.GatewayPaymentId == wrongCurrency)
            .ToDictionaryAsync(e => e.GatewayPaymentId!));
        Assert.Equal((WebhookEventStatus.Rejected, "Payment amount does not match the slot price."), (events[wrongAmount].Status, events[wrongAmount].Error));
        Assert.Equal((WebhookEventStatus.Rejected, "Payment currency does not match."), (events[wrongCurrency].Status, events[wrongCurrency].Error));
        Assert.Contains(log.Errors, line => line.Contains(wrongAmount, StringComparison.Ordinal));
        Assert.Contains(log.Errors, line => line.Contains(wrongCurrency, StringComparison.Ordinal));

        var paid = await ConfirmAsync(client, order.OrderId, "pay_right_" + Guid.NewGuid().ToString("N"));
        Assert.Equal("PendingAccept", paid.Status);
        Assert.Equal(before + 1, await CountBookingsAsync(slot.Id));
    }

    [Fact]
    public async Task A_replayed_mismatched_webhook_with_the_same_event_id_is_not_processed_again()
    {
        var log = new ErrorLog();
        using var logged = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(log)));
        var client = logged.CreateClient();
        var (token, _) = await SignUpAsync(client, "Tara Menon", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);
        var paymentId = "pay_replayed_mismatch_" + Guid.NewGuid().ToString("N");
        var eventId = "evt_replayed_mismatch_" + Guid.NewGuid().ToString("N");
        var body = CapturedBody(order.OrderId, paymentId, order.AmountPaise - 100);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var response = await PostWebhookAsync(body, SignWebhook(body), eventId, client);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False((await response.Content.ReadFromJsonAsync<WebhookBody>(Json))!.Booked);
        }

        var saved = Assert.Single(await CountAsync(db => db.WebhookEvents.AsNoTracking().Where(e => e.EventId == eventId).ToListAsync()));
        Assert.Equal(WebhookEventStatus.Rejected, saved.Status);
        Assert.Single(log.Errors, line => line.Contains(paymentId, StringComparison.Ordinal));
        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Open, await CheckoutStatusOfAsync(order.OrderId));
    }

    [Fact]
    public async Task Late_capture_after_a_failed_event_books_once_and_repeats_change_nothing()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Imran Qureshi", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);

        var failedPaymentId = "pay_failed_" + Guid.NewGuid().ToString("N");
        var failed = FailedBody(order.OrderId, failedPaymentId, order.AmountPaise);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failedResponse = await PostWebhookAsync(failed, SignWebhook(failed));
            Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
            Assert.False((await failedResponse.Content.ReadFromJsonAsync<WebhookBody>(Json))!.Booked);
        }
        Assert.Equal(before, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Failed, await CheckoutStatusOfAsync(order.OrderId));
        Assert.Equal(1, await CountAsync(db => db.WebhookEvents.CountAsync(e => e.EventId == "payment.failed:" + failedPaymentId)));

        var paymentId = "pay_studio_" + Guid.NewGuid().ToString("N");
        var eventId = "evt_" + Guid.NewGuid().ToString("N");
        var body = CapturedBody(order.OrderId, paymentId, order.AmountPaise);
        var first = await PostWebhookAsync(body, SignWebhook(body), eventId);
        var second = await PostWebhookAsync(body, SignWebhook(body), eventId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var booked = await first.Content.ReadFromJsonAsync<WebhookBody>(Json);
        var replay = await second.Content.ReadFromJsonAsync<WebhookBody>(Json);
        Assert.True(booked!.Booked);
        Assert.False(replay!.Booked);
        Assert.Equal(before + 1, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Completed, await CheckoutStatusOfAsync(order.OrderId));
        Assert.Equal(WebhookEventStatus.Processed, await EventStatusAsync(eventId));

        var duplicate = CapturedBody(order.OrderId, "pay_dup_" + Guid.NewGuid().ToString("N"), order.AmountPaise);
        var duplicateResponse = await PostWebhookAsync(duplicate, SignWebhook(duplicate), "evt_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.False((await duplicateResponse.Content.ReadFromJsonAsync<WebhookBody>(Json))!.Booked);
        var clientDuplicate = await client.PostAsJsonAsync("/api/bookings/confirm", new
        {
            orderId = order.OrderId,
            paymentId = "pay_dup_client",
            signature = SignPayment(order.OrderId, "pay_dup_client")
        });
        Assert.Equal(HttpStatusCode.Conflict, clientDuplicate.StatusCode);
        Assert.Equal(before + 1, await CountBookingsAsync(slot.Id));

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
    public async Task Failed_payment_frees_the_slot_and_a_late_capture_is_refunded_when_it_is_gone()
    {
        var late = _factory.CreateClient();
        var (lateToken, _) = await SignUpAsync(late, "Farah Siddiqui", "Female");
        late.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lateToken);
        var slot = await FirstOpenSlotAsync(late, "Studio");
        var order = await CreateOrderAsync(late, slot.Id, null, null);
        var before = await CountBookingsAsync(slot.Id);

        var failed = FailedBody(order.OrderId, "pay_failed_" + Guid.NewGuid().ToString("N"), order.AmountPaise);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(failed, SignWebhook(failed), "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);

        var other = _factory.CreateClient();
        var (otherToken, _) = await SignUpAsync(other, "Kabir Malhotra", "Male");
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var otherOrder = await CreateOrderAsync(other, slot.Id, null, null);
        await ConfirmAsync(other, otherOrder.OrderId, "pay_slot_taker_" + Guid.NewGuid().ToString("N"));

        var latePaymentId = "pay_late_" + Guid.NewGuid().ToString("N");
        var capture = CapturedBody(order.OrderId, latePaymentId, order.AmountPaise);
        var response = await PostWebhookAsync(capture, SignWebhook(capture), "evt_" + Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<WebhookBody>(Json))!.Booked);
        Assert.Equal(before + 1, await CountBookingsAsync(slot.Id));
        Assert.Equal(CheckoutStatus.Refunded, await CheckoutStatusOfAsync(order.OrderId));
        var refund = Assert.Single(RefundsFor(latePaymentId));
        Assert.Equal(order.AmountPaise, refund.AmountPaise);
    }

    [Fact]
    public async Task Refund_before_capture_is_stored_and_applied_when_the_payment_is_captured()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Nikhil Bose", "Male");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var paymentId = "pay_early_refund_" + Guid.NewGuid().ToString("N");
        var refundId = "rfnd_early_" + Guid.NewGuid().ToString("N");
        var before = await CountBookingsAsync(slot.Id);

        var refundEvent = "evt_" + Guid.NewGuid().ToString("N");
        var refund = RefundBody(RefundProcessed, refundId, paymentId, order.AmountPaise);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(refund, SignWebhook(refund), refundEvent)).StatusCode);
        Assert.Equal(WebhookEventStatus.Pending, await EventStatusAsync(refundEvent));
        Assert.Equal(before, await CountBookingsAsync(slot.Id));

        var capture = CapturedBody(order.OrderId, paymentId, order.AmountPaise);
        var captured = await PostWebhookAsync(capture, SignWebhook(capture), "evt_" + Guid.NewGuid().ToString("N"));
        var bookingId = (await captured.Content.ReadFromJsonAsync<WebhookBody>(Json))!.BookingId;

        Assert.NotNull(bookingId);
        Assert.Equal(WebhookEventStatus.Processed, await EventStatusAsync(refundEvent));
        var payment = await PaymentAsync(paymentId);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(order.Amount, payment.RefundedAmount);
        Assert.Equal(refundId, payment.GatewayRefundId);
        Assert.Equal(RefundStatus.Processed, payment.RefundStatus);
        Assert.Equal(BookingStatus.Cancelled, await CountAsync(db => db.Bookings.Where(b => b.Id == bookingId).Select(b => b.Status).SingleAsync()));
        Assert.Empty(RefundsFor(paymentId));
    }

    [Fact]
    public async Task Refund_webhook_for_our_own_cancel_is_not_applied_twice()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Tara Menon", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var paymentId = "pay_own_cancel_" + Guid.NewGuid().ToString("N");
        var booked = await ConfirmAsync(client, order.OrderId, paymentId);
        Assert.True((await client.PostAsync($"/api/bookings/{booked.Id}/cancel", null)).IsSuccessStatusCode);

        var ours = Assert.Single(RefundsFor(paymentId));
        var before = await PaymentAsync(paymentId);
        Assert.Equal(ours.RefundId, before.GatewayRefundId);
        Assert.Equal(RefundStatus.Pending, before.RefundStatus);

        var refund = RefundBody(RefundProcessed, ours.RefundId, paymentId, ours.AmountPaise);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(refund, SignWebhook(refund), "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);

        var after = await PaymentAsync(paymentId);
        Assert.Equal(before.RefundedAmount, after.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, after.Status);
        Assert.Equal(RefundStatus.Processed, after.RefundStatus);
        Assert.Single(RefundsFor(paymentId));
    }

    [Fact]
    public async Task Failed_refund_is_recorded_on_the_payment()
    {
        var client = _factory.CreateClient();
        var (token, _) = await SignUpAsync(client, "Aditi Rao", "Female");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var slot = await FirstOpenSlotAsync(client, "Studio");
        var order = await CreateOrderAsync(client, slot.Id, null, null);
        var paymentId = "pay_refund_fails_" + Guid.NewGuid().ToString("N");
        var booked = await ConfirmAsync(client, order.OrderId, paymentId);
        Assert.True((await client.PostAsync($"/api/bookings/{booked.Id}/cancel", null)).IsSuccessStatusCode);
        var ours = Assert.Single(RefundsFor(paymentId));

        var failed = RefundBody("refund.failed", ours.RefundId, paymentId, ours.AmountPaise);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(failed, SignWebhook(failed), "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);

        Assert.Equal(RefundStatus.Failed, (await PaymentAsync(paymentId)).RefundStatus);
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

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string signature, string? eventId = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/razorpay")
        {
            Content = new StringContent(body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Razorpay-Signature", signature);
        if (eventId is not null)
            request.Headers.TryAddWithoutValidation("X-Razorpay-Event-Id", eventId);
        return await (client ?? _factory.CreateClient()).SendAsync(request);
    }

    private IReadOnlyList<RecordedRefund> RefundsFor(string paymentId) =>
        Assert.IsType<FakeRazorpayClient>(_factory.Services.GetRequiredService<IRazorpayClient>())
            .Refunds.Where(r => r.PaymentId == paymentId).ToArray();

    private Task<Payment> PaymentAsync(string paymentId) =>
        CountAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.GatewayPaymentId == paymentId));

    private Task<WebhookEventStatus> EventStatusAsync(string eventId) =>
        CountAsync(db => db.WebhookEvents.Where(e => e.EventId == eventId).Select(e => e.Status).SingleAsync());

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
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true, acceptedPolicies = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await TestPreferredCity.PreferHarborAsync(client);
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

    private static string CapturedBody(string orderId, string paymentId, long paise, string currency = "INR") =>
        "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
        + "\",\"order_id\":\"" + orderId + "\",\"status\":\"captured\",\"amount\":" + paise + ",\"currency\":\"" + currency + "\"}}}}";

    private const string RefundProcessed = "refund.processed";

    private static string FailedBody(string orderId, string paymentId, long paise) =>
        "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
        + "\",\"order_id\":\"" + orderId + "\",\"status\":\"failed\",\"amount\":" + paise + ",\"currency\":\"INR\"}}}}";

    private static string RefundBody(string eventName, string refundId, string paymentId, long paise) =>
        "{\"event\":\"" + eventName + "\",\"payload\":{\"refund\":{\"entity\":{\"id\":\"" + refundId
        + "\",\"payment_id\":\"" + paymentId + "\",\"amount\":" + paise + ",\"currency\":\"INR\"}}}}";

    private static string NewPhone() => "+9197" + Random.Shared.Next(10000000, 99999999).ToString();

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
    private sealed record CheckoutBody(Guid CheckoutId, string KeyId, string OrderId, long AmountPaise, decimal Amount, string Currency, bool LocalCapture);
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

    [Theory]
    [InlineData(50000L, "INR", "captured", "order_check", null)]
    [InlineData(49900L, "INR", "captured", "order_check", "Payment amount does not match the slot price.")]
    [InlineData(50000L, "USD", "captured", "order_check", "Payment currency does not match.")]
    [InlineData(50000L, "INR", "authorized", "order_check", "Payment is not captured.")]
    [InlineData(50000L, "INR", "captured", "order_other", "Payment does not match this order.")]
    public async Task Real_gateway_callback_checks_the_captured_amount_currency_status_and_order(
        long amountPaise, string currency, string status, string remoteOrderId, string? error)
    {
        const string orderId = "order_check";
        const string paymentId = "pay_check";
        var gateway = RazorpayListPagingTests.NewClient(new PaymentHandler(new
        {
            id = paymentId, order_id = remoteOrderId, status, amount = amountPaise, amount_refunded = 0, currency
        }));
        var signature = Sign(RazorpayListPagingTests.KeySecret, $"{orderId}|{paymentId}");

        var check = () => gateway.RequireCapturedCheckoutAsync(orderId, paymentId, signature, 50000, "INR", CancellationToken.None);

        if (error is null)
            await check();
        else
            Assert.Equal(error, (await Assert.ThrowsAsync<DomainException>(check)).Message);
        Assert.Equal("Payment signature is invalid.", (await Assert.ThrowsAsync<DomainException>(() =>
            gateway.RequireCapturedCheckoutAsync(orderId, paymentId, "bad", 50000, "INR", CancellationToken.None))).Message);
    }

    /// <summary>Answers Razorpay's fetch-payment call with one payment.</summary>
    private sealed class PaymentHandler(object payment) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payment) });
    }

    /// <summary>Keeps every Error line logged by the host.</summary>
    private sealed class ErrorLog : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _errors = new();

        public IReadOnlyCollection<string> Errors => _errors.ToArray();

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                _errors.Enqueue(formatter(state, exception));
        }

        public void Dispose()
        {
        }
    }
}

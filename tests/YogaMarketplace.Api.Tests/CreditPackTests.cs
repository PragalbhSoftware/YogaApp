using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>Seeded instructor rates: Home 899, Studio 749, Online 599. Seeded terms: 15% commission, 12 h window, 50% late fee.</summary>
public class CreditPackTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";
    private const string WebhookSecret = "dev-only-not-a-live-webhook-secret";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static int _slotCounter;
    private readonly YogaApiFactory _factory;

    public CreditPackTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task An_eight_pack_books_eight_sessions_without_paying_and_the_ninth_pays()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin);
        var (customer, _) = await SignUpAsync();
        var bought = await BuyAsync(customer, pack.Id);
        Assert.Equal("Completed", bought.Status);
        Assert.Equal(8, (await CreditsAsync(customer)).Total);

        var bookings = new List<BookingBody>();
        for (var i = 0; i < 8; i++)
        {
            var slot = await SlotAsync(instructor, "Studio");
            var quote = await QuoteAsync(customer, slot);
            Assert.True(quote.Credit.Available);
            Assert.Equal(0m, quote.Credit.Total);
            Assert.Equal(749m, quote.Total);
            bookings.Add(await BookWithCreditAsync(customer, slot));
        }

        Assert.All(bookings, b => Assert.True(b.PaidWithCredit));
        Assert.All(bookings, b => Assert.Equal(600m, b.Amount));
        var bookingIds = bookings.Select(b => b.Id).ToList();
        var payments = await QueryAsync(db => db.Payments.Where(p => bookingIds.Contains(p.BookingId)).Select(p => p.Amount).ToListAsync());
        Assert.Equal(8, payments.Count);
        Assert.All(payments, amount => Assert.Equal(0m, amount));
        Assert.Equal(0, (await CreditsAsync(customer)).Total);

        var ninth = await SlotAsync(instructor, "Studio");
        var ninthQuote = await QuoteAsync(customer, ninth);
        Assert.False(ninthQuote.Credit.Available);
        Assert.Equal("NoCredits", ninthQuote.Credit.Reason);
        await AssertStatusAsync(await customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = ninth }), HttpStatusCode.Conflict);
        var paid = await PayAsync(customer, ninth);
        Assert.False(paid.PaidWithCredit);
        Assert.Equal(749m, paid.Amount);
    }

    [Fact]
    public async Task A_webhook_received_twice_adds_one_batch_and_nothing_shows_before_capture()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });

        Assert.Equal("Open", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);

        var body = CapturedBody(order.OrderId, "pay_pack_" + Guid.NewGuid().ToString("N"), order.AmountPaise);
        var first = await PostWebhookAsync(body);
        var second = await PostWebhookAsync(body);

        Assert.NotNull(first.CreditBatchId);
        Assert.Null(second.CreditBatchId);
        var status = await OrderStatusAsync(customer, order.OrderId);
        Assert.Equal("Completed", status.Status);
        Assert.Equal(first.CreditBatchId, status.Batch!.Id);
        Assert.Equal(4, (await CreditsAsync(customer)).Total);
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BatchId == first.CreditBatchId && e.Kind == CreditEntryKind.Purchase)));
    }

    [Fact]
    public async Task A_bad_signature_on_the_callback_or_the_webhook_adds_no_credits()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        var paymentId = "pay_pack_bad_sig_" + Guid.NewGuid().ToString("N");

        var callback = await customer.PostAsJsonAsync("/api/packs/confirm", new { orderId = order.OrderId, paymentId, signature = "not-a-valid-signature" });
        var webhook = await SendWebhookAsync(CapturedBody(order.OrderId, paymentId, order.AmountPaise), "deadbeef");

        await AssertStatusAsync(callback, HttpStatusCode.BadRequest);
        await AssertStatusAsync(webhook, HttpStatusCode.BadRequest);
        Assert.Equal("Open", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
        Assert.False(await QueryAsync(db => db.CreditBatches.AnyAsync(b => b.GatewayPaymentId == paymentId)));
    }

    [Fact]
    public async Task Webhook_before_the_client_callback_adds_one_batch_and_the_callback_returns_it()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        var paymentId = "pay_pack_first_" + Guid.NewGuid().ToString("N");

        var hooked = await PostWebhookAsync(CapturedBody(order.OrderId, paymentId, order.AmountPaise));
        var callback = await PostAsync<PackStatusBody>(customer, "/api/packs/confirm", new
        {
            orderId = order.OrderId,
            paymentId,
            signature = Sign(KeySecret, $"{order.OrderId}|{paymentId}")
        });

        Assert.NotNull(hooked.CreditBatchId);
        Assert.Equal(("Completed", hooked.CreditBatchId), (callback.Status, callback.Batch?.Id));
        Assert.Equal(4, (await CreditsAsync(customer)).Total);
        Assert.Equal(1, await QueryAsync(db => db.CreditBatches.CountAsync(b => b.GatewayPaymentId == paymentId)));
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BatchId == hooked.CreditBatchId && e.Kind == CreditEntryKind.Purchase)));
    }

    [Fact]
    public async Task A_captured_webhook_with_a_wrong_amount_or_currency_is_recorded_rejected_and_adds_no_credits()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        var wrongAmount = "pay_pack_amount_" + Guid.NewGuid().ToString("N");
        var wrongCurrency = "pay_pack_currency_" + Guid.NewGuid().ToString("N");

        var amountHook = await PostWebhookAsync(CapturedBody(order.OrderId, wrongAmount, order.AmountPaise + 100));
        var currencyHook = await PostWebhookAsync(CapturedBody(order.OrderId, wrongCurrency, order.AmountPaise, currency: "USD"));

        Assert.Null(amountHook.CreditBatchId);
        Assert.Null(currencyHook.CreditBatchId);
        Assert.Equal("Open", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
        var events = await QueryAsync(db => db.WebhookEvents.AsNoTracking()
            .Where(e => e.GatewayPaymentId == wrongAmount || e.GatewayPaymentId == wrongCurrency)
            .ToDictionaryAsync(e => e.GatewayPaymentId!, e => (e.Status, e.Error)));
        Assert.Equal((WebhookEventStatus.Rejected, "Payment amount does not match the pack price."), events[wrongAmount]);
        Assert.Equal((WebhookEventStatus.Rejected, "Payment currency does not match."), events[wrongCurrency]);
    }

    [Fact]
    public async Task A_replayed_mismatched_webhook_with_the_same_event_id_is_not_processed_again()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        var eventId = "evt_pack_replay_" + Guid.NewGuid().ToString("N");
        var body = CapturedBody(order.OrderId, "pay_pack_replay_" + Guid.NewGuid().ToString("N"), order.AmountPaise + 100);

        Assert.Null((await PostWebhookAsync(body, eventId)).CreditBatchId);
        const string marker = "left by the first delivery";
        await QueryAsync(db => db.WebhookEvents.Where(e => e.EventId == eventId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Error, marker)));
        Assert.Null((await PostWebhookAsync(body, eventId)).CreditBatchId);

        var saved = Assert.Single(await QueryAsync(db => db.WebhookEvents.AsNoTracking().Where(e => e.EventId == eventId).ToListAsync()));
        Assert.Equal((WebhookEventStatus.Rejected, marker), (saved.Status, saved.Error));
        Assert.Equal("Open", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
    }

    [Fact]
    public async Task A_failed_payment_marks_the_order_failed_and_a_later_capture_adds_exactly_one_batch()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });

        await PostWebhookAsync(FailedBody(order.OrderId, "pay_pack_failed_" + Guid.NewGuid().ToString("N"), order.AmountPaise));

        Assert.Equal("Failed", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);

        var capture = CapturedBody(order.OrderId, "pay_pack_retry_" + Guid.NewGuid().ToString("N"), order.AmountPaise);
        var first = await PostWebhookAsync(capture);
        var again = await PostWebhookAsync(capture);

        Assert.NotNull(first.CreditBatchId);
        Assert.Null(again.CreditBatchId);
        Assert.Equal("Completed", (await OrderStatusAsync(customer, order.OrderId)).Status);
        Assert.Equal(4, (await CreditsAsync(customer)).Total);
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BatchId == first.CreditBatchId && e.Kind == CreditEntryKind.Purchase)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_capture_after_the_pack_was_deactivated_or_repriced_gives_the_pack_as_ordered_and_logs_a_warning(bool repriced)
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 4, price: 2400m, validityDays: 30, priceCap: 800m);
        var (customer, _) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        await PostWebhookAsync(FailedBody(order.OrderId, "pay_pack_changed_failed_" + Guid.NewGuid().ToString("N"), order.AmountPaise));
        object change = repriced
            ? new { version = pack.Version, price = 3000m, sessions = 6, validityDays = 10, priceCap = 500m }
            : new { version = pack.Version, isActive = false };
        await PatchAsync<AdminPackBody>(admin, $"/api/admin/packs/{pack.Id}", change);

        var log = new WarningLog();
        await using var logged = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(log)));
        var capture = CapturedBody(order.OrderId, "pay_pack_changed_" + Guid.NewGuid().ToString("N"), order.AmountPaise);
        var response = await SendWebhookAsync(capture, Sign(WebhookSecret, capture), client: logged.CreateClient());
        var batchId = JsonSerializer.Deserialize<WebhookBody>(await response.Content.ReadAsStringAsync(), Json)!.CreditBatchId;

        Assert.NotNull(batchId);
        var batch = await QueryAsync(db => db.CreditBatches.AsNoTracking().SingleAsync(b => b.Id == batchId));
        Assert.Equal((4, 2400m, 800m, 4), (batch.Sessions, batch.Price, batch.PriceCap, batch.Remaining));
        Assert.Equal(CreditRules.ExpiresAt(batch.PurchasedAt, 30), batch.ExpiresAt);
        Assert.Equal(4, (await CreditsAsync(customer)).Total);
        Assert.Single(log.Warnings, line => line.Contains(order.OrderId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Payment_failed_marks_both_a_booking_checkout_and_a_pack_checkout_failed()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, _) = await SignUpAsync();
        var packOrder = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        var bookingOrder = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId = await SlotAsync(instructor, "Studio") });

        await PostWebhookAsync(FailedBody(packOrder.OrderId, "pay_failed_pack_" + Guid.NewGuid().ToString("N"), packOrder.AmountPaise));
        await PostWebhookAsync(FailedBody(bookingOrder.OrderId, "pay_failed_booking_" + Guid.NewGuid().ToString("N"), bookingOrder.AmountPaise));

        var statuses = await QueryAsync(db => db.CheckoutIntents.AsNoTracking()
            .Where(c => c.GatewayOrderId == packOrder.OrderId || c.GatewayOrderId == bookingOrder.OrderId)
            .ToDictionaryAsync(c => c.Kind, c => c.Status));
        Assert.Equal(CheckoutStatus.Failed, statuses[CheckoutKind.Pack]);
        Assert.Equal(CheckoutStatus.Failed, statuses[CheckoutKind.Booking]);
        Assert.Equal("Failed", (await OrderStatusAsync(customer, packOrder.OrderId)).Status);
    }

    [Fact]
    public async Task An_expired_credit_cannot_be_used_even_before_the_expiry_job_runs()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, _) = await SignUpAsync();
        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        await SetExpiryAsync(batch.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        var slot = await SlotAsync(instructor, "Studio");

        var quote = await QuoteAsync(customer, slot);

        Assert.False(quote.Credit.Available);
        Assert.Equal("NoCredits", quote.Credit.Reason);
        await AssertStatusAsync(await customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = slot }), HttpStatusCode.Conflict);
        Assert.Equal(2, await RemainingAsync(batch.Id));
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
    }

    [Fact]
    public async Task Racing_for_the_last_credit_books_once()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 1, price: 600m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var first = await SlotAsync(instructor, "Studio");
        var second = await SlotAsync(instructor, "Studio");

        var responses = await Task.WhenAll(
            customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = first }),
            customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = second }));

        Assert.Single(responses, r => r.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, Assert.Single(responses, r => !r.IsSuccessStatusCode).StatusCode);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
        var batchId = (await QueryAsync(db => db.CreditBatches.SingleAsync(b => b.PackId == pack.Id))).Id;
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BatchId == batchId && e.Kind == CreditEntryKind.Use)));
    }

    [Fact]
    public async Task Two_saves_that_both_read_the_last_credit_cannot_both_win()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 1, price: 600m);
        var (customer, customerId) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var now = DateTimeOffset.UtcNow;

        using var a = _factory.Services.CreateScope();
        using var b = _factory.Services.CreateScope();
        var batchA = await LockAndChooseAsync(a, customerId, now);
        var batchB = await LockAndChooseAsync(b, customerId, now);
        a.ServiceProvider.GetRequiredService<CreditLedger>().Use(batchA, Guid.NewGuid(), now);
        b.ServiceProvider.GetRequiredService<CreditLedger>().Use(batchB, Guid.NewGuid(), now);
        await RemoveBookingFkAsync(a, b);

        await a.ServiceProvider.GetRequiredService<YogaDbContext>().SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.ServiceProvider.GetRequiredService<YogaDbContext>().SaveChangesAsync());
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
    }

    [Fact]
    public async Task The_409_from_a_lost_credit_tells_the_customer_to_pay_instead()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 1, price: 600m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var first = await SlotAsync(instructor, "Studio");
        var second = await SlotAsync(instructor, "Studio");
        var staleQuote = await QuoteAsync(customer, second);
        Assert.True(staleQuote.Credit.Available);

        await BookWithCreditAsync(customer, first);
        var lost = await customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = second });

        Assert.Equal(HttpStatusCode.Conflict, lost.StatusCode);
        Assert.False((await QuoteAsync(customer, second)).Credit.Available);
        Assert.Equal(749m, (await PayAsync(customer, second)).Amount);
    }

    [Fact]
    public async Task Decline_and_admin_cancel_return_the_credit_and_never_twice()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 3, price: 1800m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var declined = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));
        var cancelled = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Online"));
        Assert.Equal(1, (await CreditsAsync(customer)).Total);

        await PostOkAsync(instructor, $"/api/bookings/{declined.Id}/decline");
        Assert.Equal(2, (await CreditsAsync(customer)).Total);
        await PostOkAsync(admin, $"/api/admin/bookings/{cancelled.Id}/cancel", new { reason = "Instructor unwell" });
        Assert.Equal(3, (await CreditsAsync(customer)).Total);

        await AssertStatusAsync(await admin.PostAsJsonAsync($"/api/admin/bookings/{declined.Id}/cancel", new { reason = "Again please" }), HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == declined.Id);
        var again = await Assert.ThrowsAsync<DomainException>(() =>
            scope.ServiceProvider.GetRequiredService<CreditLedger>().ReturnAsync(booking, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(409, again.StatusCode);
        Assert.Equal(3, (await CreditsAsync(customer)).Total);
        Assert.Equal(2, await QueryAsync(db2 => db2.CreditLedger.CountAsync(e =>
            (e.BookingId == declined.Id || e.BookingId == cancelled.Id) && e.Kind == CreditEntryKind.Return)));
    }

    [Fact]
    public async Task The_earliest_expiry_is_used_first_and_a_return_goes_to_its_own_batch()
    {
        var (admin, instructor) = await StaffAsync();
        var longPack = await CreatePackAsync(admin, sessions: 2, price: 1200m, validityDays: 60);
        var shortPack = await CreatePackAsync(admin, sessions: 2, price: 1200m, validityDays: 10);
        var (customer, _) = await SignUpAsync();
        var longBatch = (await BuyAsync(customer, longPack.Id)).Batch!;
        var shortBatch = (await BuyAsync(customer, shortPack.Id)).Batch!;

        var credits = await CreditsAsync(customer);
        Assert.Equal([shortBatch.Id, longBatch.Id], credits.Batches.Select(b => b.Id));

        var slot = await SlotAsync(instructor, "Studio");
        Assert.Equal(shortBatch.Id, (await QuoteAsync(customer, slot)).Credit.BatchId);
        var booking = await BookWithCreditAsync(customer, slot);
        Assert.Equal(1, await RemainingAsync(shortBatch.Id));
        Assert.Equal(2, await RemainingAsync(longBatch.Id));

        await PostOkAsync(customer, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(2, await RemainingAsync(shortBatch.Id));
        Assert.Equal(2, await RemainingAsync(longBatch.Id));
        Assert.Equal(shortBatch.ExpiresAt, (await QueryAsync(db => db.CreditBatches.SingleAsync(b => b.Id == shortBatch.Id))).ExpiresAt);
    }

    [Fact]
    public async Task A_credit_returned_to_an_expired_batch_does_not_come_back_usable()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 1, price: 600m);
        var (customer, _) = await SignUpAsync();
        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        var booking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));

        await SetExpiryAsync(batch.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        var quote = await GetAsync<CancelQuoteBody>(customer, $"/api/bookings/{booking.Id}/cancel-quote");
        Assert.True(quote.PaidWithCredit);
        Assert.False(quote.CreditReturned);
        Assert.Equal(CreditNotReturned.PackExpired, quote.CreditNotReturnedReason);
        await PostOkAsync(customer, $"/api/bookings/{booking.Id}/cancel");

        Assert.Equal(0, await RemainingAsync(batch.Id));
        Assert.Equal(0, await QueryAsync(db => db.CreditLedger
            .Where(e => e.BookingId == booking.Id && e.Kind == CreditEntryKind.Return).Select(e => e.Credits).SingleAsync()));
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
    }

    [Fact]
    public async Task The_expiry_job_expires_at_midnight_India_time_and_running_it_twice_is_safe()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 3, price: 1800m);
        var (customer, _) = await SignUpAsync();
        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        var midnight = MarketClock.SessionStart(MarketClock.Today(DateTimeOffset.UtcNow).AddDays(3), TimeOnly.MinValue);
        await SetExpiryAsync(batch.Id, midnight);

        await ExpireAsync(midnight.AddSeconds(-1));
        Assert.Equal(3, await RemainingAsync(batch.Id));

        await ExpireAsync(midnight);
        await ExpireAsync(midnight.AddMinutes(1));
        Assert.Equal(0, await RemainingAsync(batch.Id));
        var expired = await QueryAsync(db => db.CreditLedger.Where(e => e.BatchId == batch.Id && e.Kind == CreditEntryKind.Expire).ToListAsync());
        Assert.Equal(-3, Assert.Single(expired).Credits);

        using var scope = _factory.Services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<RecurringJobScheduler>();
        var now = DateTimeOffset.UtcNow;
        await scheduler.EnsureAsync(now, CancellationToken.None);
        await scheduler.EnsureAsync(now, CancellationToken.None);
        var nextMidnight = MarketClock.SessionStart(MarketClock.Today(now).AddDays(1), TimeOnly.MinValue);
        Assert.Equal(1, await QueryAsync(db => db.BackgroundJobs.CountAsync(j => j.Type == CreditExpiryJob.JobType && j.RunAt == nextMidnight)));
    }

    [Fact]
    public async Task Expiry_between_booking_and_session_follows_the_saved_setting()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (keeps, _) = await SignUpAsync();
        var keptBatch = (await BuyAsync(keeps, pack.Id)).Batch!;
        await SetExpiryAsync(keptBatch.Id, DateTimeOffset.UtcNow.AddDays(1));
        var later = await SlotAsync(instructor, "Studio", dayOffset: 5);
        var booking = await BookWithCreditAsync(keeps, later);
        await ExpireAsync(DateTimeOffset.UtcNow.AddDays(2));
        Assert.Equal(BookingStatus.PendingAccept, await QueryAsync(db => db.Bookings.Where(b => b.Id == booking.Id).Select(b => b.Status).SingleAsync()));
        Assert.Equal(0, await RemainingAsync(keptBatch.Id));

        await AdminSettingsClient.PatchAsync(admin, version => new { version, creditBookingStandsAfterExpiry = false });
        try
        {
            var (strict, _) = await SignUpAsync();
            var strictBatch = (await BuyAsync(strict, pack.Id)).Batch!;
            await SetExpiryAsync(strictBatch.Id, MarketClock.SessionStart(MarketClock.Today(DateTimeOffset.UtcNow).AddDays(4), TimeOnly.MinValue));
            var tooLate = await QuoteAsync(strict, await SlotAsync(instructor, "Studio", dayOffset: 5));
            Assert.False(tooLate.Credit.Available);
            Assert.Equal("ExpiresBeforeSession", tooLate.Credit.Reason);
            var inTime = await SlotAsync(instructor, "Studio", dayOffset: 2);
            Assert.True((await QuoteAsync(strict, inTime)).Credit.Available);
        }
        finally
        {
            await AdminSettingsClient.PatchAsync(admin, version => new { version, creditBookingStandsAfterExpiry = true });
        }
    }

    [Fact]
    public async Task Partial_refund_pays_unused_credits_minus_the_fee_and_a_second_refund_does_nothing()
    {
        var (admin, instructor) = await StaffAsync();
        await AdminSettingsClient.PatchAsync(admin, version => new { version, packReferenceSessionPrice = 799m, packRefundFee = 100m });
        var pack = await CreatePackAsync(admin);
        var (customer, customerId) = await SignUpAsync();
        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        await AdminSettingsClient.PatchAsync(admin, version => new { version, packRefundFee = 0m });
        for (var i = 0; i < 3; i++)
            await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));

        var listed = Assert.Single(await GetAsync<List<AdminBatchBody>>(admin, $"/api/admin/users/{customerId}/credits"));
        Assert.Equal(3, listed.Used);
        Assert.Equal(5, listed.Remaining);
        Assert.Equal(100m, listed.RefundFee);
        Assert.Equal(3895m, listed.RefundPreview);

        var refunded = await PostAsync<AdminBatchBody>(admin, $"/api/admin/credit-batches/{batch.Id}/refund", new { });
        Assert.Equal("Refunded", refunded.Status);
        Assert.Equal(3895m, refunded.RefundedAmount);
        Assert.Equal(0, refunded.Remaining);
        Assert.Equal(0, (await CreditsAsync(customer)).Total);

        await AssertStatusAsync(await admin.PostAsJsonAsync($"/api/admin/credit-batches/{batch.Id}/refund", new { }), HttpStatusCode.Conflict);
        var gatewayRefund = Assert.Single(RefundsFor(listed.GatewayPaymentId));
        Assert.Equal(389_500, gatewayRefund.AmountPaise);
        Assert.Equal(gatewayRefund.RefundId, await QueryAsync(db => db.CreditBatches.Where(b => b.Id == batch.Id).Select(b => b.GatewayRefundId).SingleAsync()));
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BatchId == batch.Id && e.Kind == CreditEntryKind.Refund)));
    }

    [Fact]
    public async Task Refunding_a_pack_keeps_its_upcoming_credit_booking()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, _) = await SignUpAsync();
        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        var booking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));
        await PostOkAsync(instructor, $"/api/bookings/{booking.Id}/accept");

        var refunded = await PostAsync<AdminBatchBody>(admin, $"/api/admin/credit-batches/{batch.Id}/refund", new { });
        Assert.Equal(799m, refunded.RefundedAmount);
        Assert.Equal(BookingStatus.Upcoming, await QueryAsync(db => db.Bookings.Where(b => b.Id == booking.Id).Select(b => b.Status).SingleAsync()));

        var quote = await GetAsync<CancelQuoteBody>(customer, $"/api/bookings/{booking.Id}/cancel-quote");
        Assert.True(quote.PaidWithCredit);
        Assert.False(quote.CreditReturned);
        Assert.Equal(CreditNotReturned.PackRefunded, quote.CreditNotReturnedReason);

        await PostOkAsync(customer, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(0, await RemainingAsync(batch.Id));
        Assert.Equal(0, (await CreditsAsync(customer)).Total);
    }

    [Fact]
    public async Task Reports_count_credit_bookings_apart_and_add_net_pack_sales_to_revenue()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, customerId) = await SignUpAsync();
        var before = await GetAsync<SummaryBody>(admin, "/api/admin/reports/summary");

        var batch = (await BuyAsync(customer, pack.Id)).Batch!;
        var creditBooking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));
        var paidBooking = await PayAsync(customer, await SlotAsync(instructor, "Studio"));
        var paid = await QueryAsync(db => db.Payments.Where(p => p.BookingId == paidBooking.Id).Select(p => p.Amount).SingleAsync());
        var refunded = await PostAsync<AdminBatchBody>(admin, $"/api/admin/credit-batches/{batch.Id}/refund", new { });
        var after = await GetAsync<SummaryBody>(admin, "/api/admin/reports/summary");

        Assert.Equal(before.GmvPaid + paid, after.GmvPaid);
        Assert.Equal(before.PaidBookings + 1, after.PaidBookings);
        Assert.Equal(before.CreditBookings + 1, after.CreditBookings);
        Assert.Equal(before.PackSales + 1200m - refunded.RefundedAmount, after.PackSales);
        Assert.Equal(after.GmvPaid + after.PackSales, after.Revenue);

        var payments = await GetAsync<List<PaymentRowBody>>(admin, "/api/admin/payments");
        Assert.DoesNotContain(payments, p => p.BookingId == creditBooking.Id);
        Assert.Contains(payments, p => p.BookingId == paidBooking.Id && p.Kind == "Booking");
        var packRow = Assert.Single(payments, p => p.Id == batch.Id);
        Assert.Equal("Pack", packRow.Kind);
        Assert.Equal(1200m, packRow.Amount);
        Assert.Equal(refunded.RefundedAmount, packRow.RefundedAmount);
        Assert.Equal("PartiallyRefunded", packRow.Status);
        Assert.Equal(customerId, packRow.CustomerId);
        Assert.Null(packRow.ProviderId);

        var paidOnly = await GetAsync<List<PaymentRowBody>>(admin, "/api/admin/payments?status=Paid");
        Assert.DoesNotContain(paidOnly, p => p.Id == batch.Id);
    }

    [Fact]
    public async Task Settings_changed_after_purchase_keep_the_saved_rules()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin);
        var (before, _) = await SignUpAsync();
        await BuyAsync(before, pack.Id);

        await AdminSettingsClient.PatchAsync(admin, version => new
        {
            version,
            convenienceFee = 25m,
            creditConvenienceFee = true,
            creditPayBasis = "SessionPrice"
        });
        try
        {
            var oldRules = await SlotAsync(instructor, "Studio");
            Assert.Equal(0m, (await QuoteAsync(before, oldRules)).Credit.Total);
            var booked = await BookWithCreditAsync(before, oldRules);
            Assert.Equal(600m, booked.Amount);

            var (after, _) = await SignUpAsync();
            await BuyAsync(after, pack.Id);
            var newRules = await SlotAsync(instructor, "Studio");
            var quote = await QuoteAsync(after, newRules);
            Assert.Equal(25m, quote.Credit.ConvenienceFee);
            await AssertStatusAsync(await after.PostAsJsonAsync("/api/bookings/credit", new { slotId = newRules }), HttpStatusCode.Conflict);

            var order = await PostAsync<OrderBody>(after, "/api/bookings/orders", new { slotId = newRules, useCredit = true });
            Assert.Equal(2500, order.AmountPaise);
            var feeBooking = await PostAsync<BookingBody>(after, "/api/bookings/local-confirm", new { orderId = order.OrderId });
            Assert.True(feeBooking.PaidWithCredit);
            Assert.Equal(749m, feeBooking.Amount);
            Assert.Equal(7, (await CreditsAsync(after)).Total);
        }
        finally
        {
            await AdminSettingsClient.PatchAsync(admin, version => new
            {
                version,
                convenienceFee = 0m,
                creditConvenienceFee = false,
                creditPayBasis = "PackPerSession"
            });
        }
    }

    [Fact]
    public async Task A_credit_payout_uses_the_frozen_amount_after_price_and_commission_changes()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var booking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"));
        await PostOkAsync(instructor, $"/api/bookings/{booking.Id}/accept");

        await AdminSettingsClient.PatchAsync(admin, version => new { version, commissionPercent = 30m });
        var rates = await instructor.PatchAsJsonAsync("/api/providers/me/rates", new { studioRate = 999m });
        Assert.True(rates.IsSuccessStatusCode, await rates.Content.ReadAsStringAsync());
        try
        {
            await PostOkAsync(instructor, $"/api/bookings/{booking.Id}/complete");
            var payout = await QueryAsync(db => db.PayoutsPending.SingleAsync(p => p.BookingId == booking.Id));
            Assert.Equal(600m, payout.GrossAmount);
            Assert.Equal(15m, payout.FeePercent);
            Assert.Equal(510m, payout.NetAmount);
        }
        finally
        {
            await instructor.PatchAsJsonAsync("/api/providers/me/rates", new { studioRate = 749m });
            await AdminSettingsClient.ResetFeesAsync(admin);
        }
    }

    [Fact]
    public async Task A_late_cancel_uses_the_credit_and_pays_the_instructor_their_share()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        await AdminSettingsClient.SetCancelWindowAsync(admin, 168);
        try
        {
            var booking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio", dayOffset: 2));
            await PostOkAsync(instructor, $"/api/bookings/{booking.Id}/accept");

            var quote = await GetAsync<CancelQuoteBody>(customer, $"/api/bookings/{booking.Id}/cancel-quote");
            Assert.False(quote.CreditReturned);
            Assert.Equal(CreditNotReturned.LateCancel, quote.CreditNotReturnedReason);
            Assert.Equal(0m, quote.Refund);
            await PostOkAsync(customer, $"/api/bookings/{booking.Id}/cancel");

            Assert.Equal(1, (await CreditsAsync(customer)).Total);
            var payout = await QueryAsync(db => db.PayoutsPending.SingleAsync(p => p.BookingId == booking.Id));
            Assert.Equal(300m, payout.GrossAmount);
            Assert.Equal(255m, payout.NetAmount);
        }
        finally
        {
            await AdminSettingsClient.ResetFeesAsync(admin);
        }
    }

    [Fact]
    public async Task A_reschedule_uses_no_extra_credit()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);
        var booking = await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio", dayOffset: 3));
        await PostOkAsync(instructor, $"/api/bookings/{booking.Id}/accept");

        await PostOkAsync(customer, $"/api/bookings/{booking.Id}/reschedule", new { slotId = await SlotAsync(instructor, "Studio", dayOffset: 4) });

        Assert.Equal(1, (await CreditsAsync(customer)).Total);
        Assert.Equal(1, await QueryAsync(db => db.CreditLedger.CountAsync(e => e.BookingId == booking.Id)));
    }

    [Fact]
    public async Task A_session_above_the_cap_and_a_trial_are_paid_normally()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, priceCap: 800m);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);

        var home = await SlotAsync(instructor, "Home");
        var homeQuote = await QuoteAsync(customer, home);
        Assert.False(homeQuote.Credit.Available);
        Assert.Equal("AboveCap", homeQuote.Credit.Reason);
        var address = new { slotId = home, homeAddress = "12 Hill Road", landmark = "Near the park" };
        await AssertStatusAsync(await customer.PostAsJsonAsync("/api/bookings/credit", address), HttpStatusCode.Conflict);
        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", address);
        Assert.Equal(89_900, order.AmountPaise);

        await PutOkAsync(instructor, "/api/providers/me/trial", new { enabled = true, price = 199m });
        try
        {
            var slot = await SlotAsync(instructor, "Studio");
            var trialQuote = await GetAsync<QuoteBody>(customer, $"/api/bookings/quote?slotId={slot}&trial=true");
            Assert.False(trialQuote.Credit.Available);
            Assert.Equal("Trial", trialQuote.Credit.Reason);
            await AssertStatusAsync(await customer.PostAsJsonAsync("/api/bookings/credit", new { slotId = slot, trial = true }), HttpStatusCode.Conflict);
            await AssertStatusAsync(await customer.PostAsJsonAsync("/api/bookings/orders", new { slotId = slot, trial = true, useCredit = true }), HttpStatusCode.Conflict);
            Assert.Equal(8, (await CreditsAsync(customer)).Total);
        }
        finally
        {
            await PutOkAsync(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    [Fact]
    public async Task A_blocked_customer_cannot_buy_and_a_late_capture_is_refunded()
    {
        var (admin, _) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        var (customer, customerId) = await SignUpAsync();
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });

        await PostOkAsync(admin, $"/api/admin/users/{customerId}/block", new { reason = "Chargeback abuse" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.PostAsJsonAsync($"/api/packs/{pack.Id}/orders", new { })).StatusCode);

        var paymentId = "pay_blocked_" + Guid.NewGuid().ToString("N");
        var result = await PostWebhookAsync(CapturedBody(order.OrderId, paymentId, order.AmountPaise));
        Assert.Null(result.CreditBatchId);
        Assert.Equal(CheckoutStatus.Refunded, await QueryAsync(db => db.CheckoutIntents.Where(c => c.GatewayOrderId == order.OrderId).Select(c => c.Status).SingleAsync()));
        Assert.Single(RefundsFor(paymentId));
        Assert.Equal(0, await QueryAsync(db => db.CreditBatches.CountAsync(b => b.CustomerId == customerId)));
    }

    [Fact]
    public async Task Admin_packs_deactivate_instead_of_delete_and_bought_credits_stay_usable()
    {
        var (admin, instructor) = await StaffAsync();
        var pack = await CreatePackAsync(admin, sessions: 2, price: 1200m);
        Assert.Contains(await _factory.CreateClient().GetFromJsonAsync<List<PackBody>>("/api/packs", Json) ?? [], p => p.Id == pack.Id);
        var (customer, _) = await SignUpAsync();
        await BuyAsync(customer, pack.Id);

        var stale = await admin.PatchAsJsonAsync($"/api/admin/packs/{pack.Id}", new { version = pack.Version + 5, isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var off = await PatchAsync<AdminPackBody>(admin, $"/api/admin/packs/{pack.Id}", new { version = pack.Version, isActive = false });
        Assert.False(off.IsActive);
        Assert.Equal(1, off.Sold);

        Assert.DoesNotContain(await _factory.CreateClient().GetFromJsonAsync<List<PackBody>>("/api/packs", Json) ?? [], p => p.Id == pack.Id);
        await AssertStatusAsync(await customer.PostAsJsonAsync($"/api/packs/{pack.Id}/orders", new { }), HttpStatusCode.NotFound);
        Assert.True((await BookWithCreditAsync(customer, await SlotAsync(instructor, "Studio"))).PaidWithCredit);

        var invalid = await admin.PostAsJsonAsync("/api/admin/packs", new { name = "Bad", sessions = 0, price = 100m, validityDays = 30, priceCap = 800m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/packs")).StatusCode);
    }

    [Fact]
    public async Task The_instructor_picks_which_service_carries_the_trial()
    {
        var (_, instructor) = await StaffAsync();
        var services = await GetAsync<List<ServiceBody>>(instructor, "/api/providers/me/services");
        var service = Assert.Single(services);
        Assert.Equal(SeedIds.AnanyaServiceId, service.Id);
        Assert.False(service.TrialEnabled);

        var unknown = await instructor.PutAsJsonAsync("/api/providers/me/trial", new { serviceId = Guid.NewGuid(), enabled = true, price = 99m });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        try
        {
            var trial = await PutOkAsync(instructor, "/api/providers/me/trial", new { serviceId = service.Id, enabled = true, price = 99m });
            Assert.Equal(service.Id, trial.ServiceId);
            var listed = Assert.Single(await GetAsync<List<ServiceBody>>(instructor, "/api/providers/me/services"));
            Assert.True(listed.TrialEnabled);
            Assert.Equal(99m, listed.TrialPrice);
        }
        finally
        {
            await PutOkAsync(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    private async Task<CreditBatch> LockAndChooseAsync(IServiceScope scope, Guid customerId, DateTimeOffset now)
    {
        var ledger = scope.ServiceProvider.GetRequiredService<CreditLedger>();
        await ledger.LockAsync(customerId, now, CancellationToken.None);
        var choice = await ledger.ChooseAsync(customerId, 749m, now.AddDays(1), now, CancellationToken.None);
        return Assert.IsType<CreditBatch>(choice.Batch);
    }

    /// <summary>The ledger rows point at bookings that these scopes never create, so drop the booking link.</summary>
    private static Task RemoveBookingFkAsync(params IServiceScope[] scopes)
    {
        foreach (var scope in scopes)
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            foreach (var entry in db.ChangeTracker.Entries<CreditLedgerEntry>())
                entry.Entity.BookingId = null;
        }
        return Task.CompletedTask;
    }

    private async Task<(HttpClient Admin, HttpClient Instructor)> StaffAsync() =>
        (await SignInAsync(SeedIds.AdminPhone), await SignInAsync(SeedIds.AnanyaPhone));

    private static async Task<AdminPackBody> CreatePackAsync(
        HttpClient admin,
        int sessions = 8,
        decimal price = 4800m,
        int validityDays = 30,
        decimal priceCap = 800m) =>
        await PostAsync<AdminPackBody>(admin, "/api/admin/packs", new
        {
            name = $"{sessions}-session pack {Guid.NewGuid():N}"[..30],
            sessions,
            price,
            validityDays,
            priceCap
        });

    private static async Task<PackStatusBody> BuyAsync(HttpClient customer, Guid packId)
    {
        var order = await PostAsync<PackOrderBody>(customer, $"/api/packs/{packId}/orders", new { });
        return await PostAsync<PackStatusBody>(customer, "/api/packs/local-confirm", new { orderId = order.OrderId });
    }

    private static Task<PackStatusBody> OrderStatusAsync(HttpClient customer, string orderId) =>
        GetAsync<PackStatusBody>(customer, $"/api/packs/orders/{orderId}");

    private static Task<CreditsBody> CreditsAsync(HttpClient customer) => GetAsync<CreditsBody>(customer, "/api/credits/me");

    private static Task<QuoteBody> QuoteAsync(HttpClient customer, Guid slotId) =>
        GetAsync<QuoteBody>(customer, $"/api/bookings/quote?slotId={slotId}");

    private static Task<BookingBody> BookWithCreditAsync(HttpClient customer, Guid slotId) =>
        PostAsync<BookingBody>(customer, "/api/bookings/credit", new { slotId });

    private static async Task<BookingBody> PayAsync(HttpClient customer, Guid slotId)
    {
        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId });
        return await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = order.OrderId });
    }

    /// <summary>Each call gets its own start time at :05 or :35, so it never clashes with the seeded on-the-hour slots.</summary>
    private static async Task<Guid> SlotAsync(HttpClient instructor, string mode, int dayOffset = 1)
    {
        var n = Interlocked.Increment(ref _slotCounter);
        var day = MarketClock.Today(DateTimeOffset.UtcNow).AddDays(dayOffset + n / 46);
        var start = new TimeOnly(0, 5).AddMinutes(30 * (n % 46));
        var created = await PostAsync<List<SlotBody>>(instructor, "/api/providers/me/slots", new
        {
            mode,
            slots = new[] { new { date = day, start = start.ToString("HH:mm"), end = start.AddMinutes(25).ToString("HH:mm") } }
        });
        return Assert.Single(created).Id;
    }

    private async Task ExpireAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CreditExpiryJob>().ExpireDueAsync(now, CancellationToken.None);
    }

    private async Task SetExpiryAsync(Guid batchId, DateTimeOffset expiresAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var batch = await db.CreditBatches.SingleAsync(b => b.Id == batchId);
        batch.ExpiresAt = expiresAt;
        await db.SaveChangesAsync();
    }

    private Task<int> RemainingAsync(Guid batchId) =>
        QueryAsync(db => db.CreditBatches.Where(b => b.Id == batchId).Select(b => b.Remaining).SingleAsync());

    private async Task<WebhookBody> PostWebhookAsync(string body, string? eventId = null)
    {
        var response = await SendWebhookAsync(body, Sign(WebhookSecret, body), eventId);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<WebhookBody>(payload, Json)!;
    }

    private Task<HttpResponseMessage> SendWebhookAsync(string body, string signature, string? eventId = null, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/razorpay")
        {
            Content = new StringContent(body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Razorpay-Signature", signature);
        if (eventId is not null)
            request.Headers.TryAddWithoutValidation("X-Razorpay-Event-Id", eventId);
        return (client ?? _factory.CreateClient()).SendAsync(request);
    }

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string CapturedBody(string orderId, string paymentId, long paise, string currency = "INR") =>
        "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
        + "\",\"order_id\":\"" + orderId + "\",\"status\":\"captured\",\"amount\":" + paise + ",\"currency\":\"" + currency + "\"}}}}";

    private static string FailedBody(string orderId, string paymentId, long paise) =>
        "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
        + "\",\"order_id\":\"" + orderId + "\",\"status\":\"failed\",\"amount\":" + paise + ",\"currency\":\"INR\"}}}}";

    private IReadOnlyList<RecordedRefund> RefundsFor(string paymentId) =>
        Assert.IsType<FakeRazorpayClient>(_factory.Services.GetRequiredService<IRazorpayClient>())
            .Refunds.Where(r => r.PaymentId == paymentId).ToArray();

    private async Task<(HttpClient Client, Guid UserId)> SignUpAsync()
    {
        var client = _factory.CreateClient();
        var phone = "+9193" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Meera Pillai", gender = "Female", isNewUser = true, acceptedPolicies = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.User.Id);
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<T> QueryAsync<T>(Func<YogaDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected) =>
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private static async Task PostOkAsync(HttpClient client, string url, object? body = null)
    {
        var response = await client.PostAsJsonAsync(url, body ?? new { });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TrialBody> PutOkAsync(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<TrialBody>(payload, Json)!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record OtpBody(string DevCode);
    private sealed record UserBody(Guid Id);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record SlotBody(Guid Id);
    private sealed record OrderBody(string OrderId, long AmountPaise);
    private sealed record PackOrderBody(string OrderId, long AmountPaise);
    private sealed record BatchBody(Guid Id, int Remaining, DateTimeOffset ExpiresAt);
    private sealed record PackStatusBody(string OrderId, string Status, BatchBody? Batch);
    private sealed record CreditsBody(int Total, List<BatchBody> Batches);
    private sealed record CreditBody(bool Available, string? Reason, decimal ConvenienceFee, decimal Total, Guid? BatchId);
    private sealed record QuoteBody(decimal Total, CreditBody Credit);
    private sealed record BookingBody(Guid Id, decimal Amount, bool PaidWithCredit);
    private sealed record CancelQuoteBody(decimal Refund, bool PaidWithCredit, bool CreditReturned, string? CreditNotReturnedReason);
    private sealed record WebhookBody(bool Booked, Guid? CreditBatchId);
    private sealed record SummaryBody(decimal GmvPaid, decimal PackSales, decimal Revenue, int PaidBookings, int CreditBookings);
    private sealed record PaymentRowBody(
        Guid Id, Guid? BookingId, decimal Amount, string Status, Guid? ProviderId, Guid CustomerId, decimal RefundedAmount, string Kind);
    private sealed record AdminPackBody(Guid Id, int Version, bool IsActive, int Sold);
    private sealed record PackBody(Guid Id);
    private sealed record AdminBatchBody(
        Guid Id, int Remaining, int Used, string Status, decimal RefundFee, decimal RefundPreview, decimal RefundedAmount, string GatewayPaymentId);
    private sealed record TrialBody(Guid? ServiceId, bool Enabled, decimal? Price);
    private sealed record ServiceBody(Guid Id, bool TrialEnabled, decimal? TrialPrice);

    /// <summary>Keeps the Warning lines logged by the pack purchase service.</summary>
    private sealed class WarningLog : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _warnings = new();

        public IReadOnlyCollection<string> Warnings => _warnings.ToArray();

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(CreditPurchaseService).FullName ? this : NullLogger.Instance;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                _warnings.Enqueue(formatter(state, exception));
        }

        public void Dispose()
        {
        }
    }
}

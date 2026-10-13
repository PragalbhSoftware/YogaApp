using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// Razorpay webhooks. Every delivery is logged once per event id before it is processed, so a redelivery returns 200
/// and changes nothing. Razorpay retries anything that is not 2xx, so outcomes it cannot fix are answered with 200.
/// </summary>
public partial class BookingService
{
    private const string LateCaptureRefundedMessage =
        "This payment arrived after the order failed and the slot can no longer be booked. We refunded it in full.";
    private const string RefundedInGatewayReason = "Refunded in Razorpay.";

    public async Task<WebhookResult> HandleWebhookAsync(string rawBody, string? signature, string? eventId, CancellationToken cancellationToken)
    {
        if (!_razorpay.VerifyWebhookSignature(rawBody, signature))
            throw new DomainException("Webhook signature is invalid.");

        var envelope = RazorpayWebhookParser.ReadEnvelope(rawBody);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            eventId = FallbackEventId(envelope, rawBody);
            _logger.LogWarning("Razorpay webhook {EventType} came without an event id header; using {EventId}.", envelope.Event, eventId);
        }

        var rowId = await StartEventAsync(eventId.Trim(), envelope, rawBody, cancellationToken);
        if (rowId is not Guid id)
        {
            _logger.LogInformation("Razorpay webhook {EventId} ({EventType}) was already received.", eventId, envelope.Event);
            return WebhookResult.Ignored();
        }

        try
        {
            var (result, status, error) = await DispatchAsync(envelope.Event, rawBody, cancellationToken);
            await FinishEventAsync(id, status, error, CancellationToken.None);
            return result;
        }
        catch (Exception ex)
        {
            await FinishEventAsync(id, WebhookEventStatus.Failed, ex.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task<(WebhookResult Result, WebhookEventStatus Status, string? Error)> DispatchAsync(
        string eventType,
        string rawBody,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case RazorpayWire.PaymentCapturedEvent:
                try
                {
                    return (await HandleCapturedAsync(rawBody, cancellationToken), WebhookEventStatus.Processed, null);
                }
                catch (PaymentMismatchException ex)
                {
                    // Razorpay would redeliver forever; answer 200 and leave the payment for reconciliation to report.
                    _logger.LogError(ex, "Captured Razorpay payment {PaymentId} does not match its order and was not applied.",
                        RazorpayWebhookParser.ReadEnvelope(rawBody).PaymentId);
                    return (WebhookResult.Ignored(), WebhookEventStatus.Rejected, ex.Message);
                }
            case RazorpayWire.PaymentFailedEvent:
                await HandlePaymentFailedAsync(rawBody, cancellationToken);
                return (WebhookResult.Ignored(), WebhookEventStatus.Processed, null);
            case RazorpayWire.RefundProcessedEvent:
                var applied = await ApplyRefundProcessedAsync(RazorpayWebhookParser.ReadRefund(rawBody), cancellationToken);
                return (WebhookResult.Ignored(), applied ? WebhookEventStatus.Processed : WebhookEventStatus.Pending, null);
            case RazorpayWire.RefundFailedEvent:
                return (WebhookResult.Ignored(), WebhookEventStatus.Processed, await HandleRefundFailedAsync(rawBody, cancellationToken));
            default:
                return (WebhookResult.Ignored(), WebhookEventStatus.Processed, null);
        }
    }

    private async Task<WebhookResult> HandleCapturedAsync(string rawBody, CancellationToken cancellationToken)
    {
        if (!RazorpayWebhookParser.TryReadCapturedPayment(rawBody, out var captured))
            return WebhookResult.Ignored();

        var kind = await _db.CheckoutIntents.AsNoTracking()
            .Where(c => c.GatewayOrderId == captured.OrderId)
            .Select(c => (CheckoutKind?)c.Kind)
            .SingleOrDefaultAsync(cancellationToken);
        if (kind == CheckoutKind.Pack)
            return await _packs.HandleCapturedWebhookAsync(captured, cancellationToken);

        try
        {
            var booking = await CaptureAsync(
                captured.OrderId,
                captured.PaymentId,
                expectedCustomerId: null,
                captured.AmountPaise,
                captured.Currency,
                cancellationToken);
            return WebhookResult.ForBooking(booking.Id);
        }
        catch (DomainException ex) when (ex.StatusCode is 404 or 409)
        {
            // Already paid, already refunded, or the slot is gone. Answer 200 so Razorpay stops retrying.
            _logger.LogWarning(ex, "Captured Razorpay payment {PaymentId} for order {OrderId} did not create a booking.", captured.PaymentId, captured.OrderId);
            return WebhookResult.Ignored();
        }
    }

    /// <summary>An open checkout never holds the slot, so marking it failed is all that frees it.</summary>
    private async Task HandlePaymentFailedAsync(string rawBody, CancellationToken cancellationToken)
    {
        if (RazorpayWebhookParser.ReadFailedPayment(rawBody) is not FailedRazorpayPayment failed)
            return;

        var updated = await _db.CheckoutIntents
            .Where(c => c.GatewayOrderId == failed.OrderId && c.Status == CheckoutStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CheckoutStatus.Failed), cancellationToken);
        if (updated > 0)
            _logger.LogInformation("Razorpay payment {PaymentId} failed; checkout for order {OrderId} marked failed.", failed.PaymentId, failed.OrderId);
    }

    /// <summary>
    /// Records a Razorpay refund on the booking payment. A refund we started ourselves already carries its id and is skipped.
    /// Returns false when the payment is not captured here yet, so the event waits for <see cref="ApplyPendingRefundsAsync"/>.
    /// </summary>
    private async Task<bool> ApplyRefundProcessedAsync(RazorpayRefund refund, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .SingleOrDefaultAsync(p => p.GatewayPaymentId == refund.PaymentId, cancellationToken);
        if (payment is null)
            return await RefundedOutsideBookingsAsync(refund.PaymentId, cancellationToken);

        if (payment.GatewayRefundId == refund.RefundId)
        {
            if (payment.RefundStatus != RefundStatus.Processed)
            {
                payment.RefundStatus = RefundStatus.Processed;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        if (payment.Status != PaymentStatus.Paid)
        {
            _logger.LogWarning(
                "Razorpay refund {RefundId} on payment {PaymentId} for booking {BookingId} does not match the refund already recorded.",
                refund.RefundId,
                refund.PaymentId,
                payment.BookingId);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        PaymentRules.Refund(payment, RazorpayMoney.FromPaise(refund.AmountPaise));
        payment.GatewayRefundId = refund.RefundId;
        payment.RefundStatus = RefundStatus.Processed;
        if (payment is { Status: PaymentStatus.Refunded, Booking: { CreditBatchId: null, Status: BookingStatus.PendingAccept or BookingStatus.Upcoming } booking })
            BookingRules.AdminCancel(booking, RefundedInGatewayReason, now);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Recorded Razorpay refund {RefundId} of {AmountPaise} paise on payment {PaymentId} for booking {BookingId}.",
            refund.RefundId,
            refund.AmountPaise,
            refund.PaymentId,
            payment.BookingId);
        return true;
    }

    /// <summary>Checkout refunds (a second trial, a late capture) and pack sales have no booking payment to update.</summary>
    private async Task<bool> RefundedOutsideBookingsAsync(string paymentId, CancellationToken cancellationToken) =>
        await _db.CheckoutIntents.AnyAsync(c => c.GatewayPaymentId == paymentId && c.Status == CheckoutStatus.Refunded, cancellationToken)
        || await _db.CreditBatches.AnyAsync(b => b.GatewayPaymentId == paymentId, cancellationToken);

    private async Task<string> HandleRefundFailedAsync(string rawBody, CancellationToken cancellationToken)
    {
        var refund = RazorpayWebhookParser.ReadRefund(rawBody);
        var payment = await _db.Payments.SingleOrDefaultAsync(p => p.GatewayPaymentId == refund.PaymentId, cancellationToken);
        if (payment is not null && (payment.GatewayRefundId is null || payment.GatewayRefundId == refund.RefundId))
        {
            payment.GatewayRefundId = refund.RefundId;
            payment.RefundStatus = RefundStatus.Failed;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogError(
            "Razorpay refund {RefundId} of {AmountPaise} paise failed for booking {BookingId}, payment {PaymentId}.",
            refund.RefundId,
            refund.AmountPaise,
            payment?.BookingId,
            refund.PaymentId);
        return "Refund failed at Razorpay.";
    }

    /// <summary>Runs after a capture commits. A failure is logged and the events stay pending, so the booking still succeeds.</summary>
    private async Task ApplyPendingRefundsAsync(string paymentId, CancellationToken cancellationToken)
    {
        try
        {
            var pending = await _db.WebhookEvents.AsNoTracking()
                .Where(e => e.Provider == PaymentGateways.Razorpay && e.GatewayPaymentId == paymentId && e.Status == WebhookEventStatus.Pending)
                .Select(e => new { e.Id, e.Payload, e.ReceivedAt })
                .ToListAsync(cancellationToken);
            foreach (var row in pending.OrderBy(e => e.ReceivedAt))
            {
                if (await ApplyRefundProcessedAsync(RazorpayWebhookParser.ReadRefund(row.Payload), cancellationToken))
                    await FinishEventAsync(row.Id, WebhookEventStatus.Processed, null, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or DomainException)
        {
            _logger.LogError(ex, "Could not apply pending Razorpay refunds for payment {PaymentId}.", paymentId);
        }
    }

    /// <summary>
    /// Inserts the delivery and returns its id, or null when it was already received. A delivery that failed earlier
    /// is claimed back, so Razorpay's retry can finish it.
    /// </summary>
    private async Task<Guid?> StartEventAsync(string eventId, RazorpayWebhookEnvelope envelope, string rawBody, CancellationToken cancellationToken)
    {
        var row = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = PaymentGateways.Razorpay,
            EventId = eventId,
            EventType = envelope.Event,
            GatewayPaymentId = envelope.PaymentId,
            Payload = rawBody,
            ReceivedAt = DateTimeOffset.UtcNow
        };
        _db.WebhookEvents.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return row.Id;
        }
        catch (DbUpdateException)
        {
            _db.Entry(row).State = EntityState.Detached;
            var existing = _db.WebhookEvents.Where(e => e.Provider == PaymentGateways.Razorpay && e.EventId == eventId);
            if (!await existing.AnyAsync(cancellationToken))
                throw;

            var claimed = await existing
                .Where(e => e.Status == WebhookEventStatus.Failed)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, WebhookEventStatus.Received), cancellationToken);
            return claimed == 0 ? null : await existing.Select(e => e.Id).SingleAsync(cancellationToken);
        }
    }

    private Task FinishEventAsync(Guid id, WebhookEventStatus status, string? error, CancellationToken cancellationToken)
    {
        var trimmed = error is { Length: > 500 } ? error[..500] : error;
        DateTimeOffset? processedAt = status == WebhookEventStatus.Processed ? DateTimeOffset.UtcNow : null;
        return _db.WebhookEvents
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Status, status)
                    .SetProperty(e => e.Error, trimmed)
                    .SetProperty(e => e.ProcessedAt, processedAt),
                cancellationToken);
    }

    private static string FallbackEventId(RazorpayWebhookEnvelope envelope, string rawBody) =>
        string.IsNullOrWhiteSpace(envelope.EntityId)
            ? "body:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant()
            : $"{envelope.Event}:{envelope.EntityId}";
}

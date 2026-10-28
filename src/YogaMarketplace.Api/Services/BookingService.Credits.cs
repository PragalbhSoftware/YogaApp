using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// Booking with a credit. With no convenience fee to pay the booking is made at once; with a fee it goes through
/// Razorpay for the fee only and the credit is used at capture. Instructor decline and admin cancel always return the
/// credit; a customer cancel follows <see cref="CreditRules.SettleCustomerCancel"/>.
/// </summary>
public partial class BookingService
{
    private const string CreditFeeRefundedMessage = CreditRules.LastCreditTakenMessage + " We refunded the fee.";
    private const string CreditBlockedRefundedMessage = CreditRules.BlockedMessage + " We refunded the fee.";

    public async Task<BookingResponse> CreateCreditBookingAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        if (customer.IsBlocked)
            throw new DomainException(CreditRules.BlockedMessage, 403);
        if (request.Trial)
            throw new DomainException("A trial class can't be paid with a credit.", 409);

        var slot = await LoadBookableSlotAsync(request.SlotId, cancellationToken);
        var provider = slot.Provider!;
        EnsureHomeCityMatches(customer, slot);
        var (homeAddress, landmark) = ResolveLocation(customer, slot, request);
        var service = await ResolveServiceAsync(provider.Id, request.ServiceId, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        var rate = RateOf(slot);
        var now = _time.GetUtcNow();
        await _credits.LockAsync(customer.Id, now, cancellationToken);
        var batch = await RequireCreditAsync(customer.Id, slot, rate, now, cancellationToken);
        if (CreditFeeFor(batch, settings.Terms) > 0)
            throw new DomainException("This credit booking has a convenience fee. Pay it to book.", 409);

        var booking = BookingRules.CreateAfterPayment(
            customer.Id,
            service,
            slot,
            CreditRules.InstructorGross(batch, rate),
            settings.Terms with { ConvenienceFee = 0m },
            homeAddress,
            landmark,
            provider.GoogleMeetLink,
            provider.StudioAddress,
            now);
        CreditRules.Attach(booking, batch);
        _credits.Use(batch, booking.Id, now);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 0m,
            Status = PaymentStatus.Pending,
            Gateway = PaymentGateways.Credit,
            CreatedAt = now
        };
        PaymentRules.MarkPaid(payment, null, now);
        _db.Services.Attach(service);
        booking.Provider = provider;
        booking.Service = service;
        booking.Slot = slot;
        booking.Payment = payment;
        _db.Bookings.Add(booking);
        await _notifications.BookedAsync(booking, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Credit booking conflict for customer {CustomerId} on slot {SlotId}.", customer.Id, slot.Id);
            _db.ChangeTracker.Clear();
            var stillCovered = (await _credits.ChooseAsync(customer.Id, rate, SessionStartOf(slot), _time.GetUtcNow(), cancellationToken)).Batch;
            throw new DomainException(stillCovered is null ? CreditRules.LastCreditTakenMessage : "That slot was just booked.", 409);
        }

        _logger.LogInformation(
            "Customer {CustomerId} booked {BookingId} with a credit from batch {BatchId}.", customer.Id, booking.Id, batch.Id);
        return ToResponse(booking, payment, settings.Currency);
    }

    private async Task<CreditOptionResponse> CreditOptionAsync(
        User customer,
        AvailabilitySlot slot,
        bool trial,
        BookingTerms terms,
        CancellationToken cancellationToken)
    {
        if (trial)
            return Unavailable(CreditUnavailable.Trial, "A trial class can't be paid with a credit.");
        if (customer.IsBlocked)
            return Unavailable(CreditUnavailable.Blocked, CreditRules.BlockedMessage);

        var choice = await _credits.ChooseAsync(customer.Id, RateOf(slot), SessionStartOf(slot), _time.GetUtcNow(), cancellationToken);
        if (choice.Batch is not CreditBatch batch)
            return Unavailable(choice.Reason!.Value, UnavailableMessage(choice.Reason!.Value));

        var fee = CreditFeeFor(batch, terms);
        return new CreditOptionResponse(true, null, null, fee, fee, batch.Id, batch.PackName, CreditRules.LastDay(batch));
    }

    private static CreditOptionResponse Unavailable(CreditUnavailable reason, string message) =>
        new(false, reason.ToString(), message, 0m, 0m, null, null, null);

    private static string UnavailableMessage(CreditUnavailable reason) => reason switch
    {
        CreditUnavailable.AboveCap => "This session costs more than your pack covers.",
        CreditUnavailable.ExpiresBeforeSession => "Your credits expire before this session.",
        _ => CreditRules.NoneLeftMessage
    };

    /// <summary>The fee only applies when the batch was bought with the fee rule on and it reaches Razorpay's ₹1 minimum.</summary>
    private static decimal CreditFeeFor(CreditBatch batch, BookingTerms terms) =>
        batch.ChargeConvenienceFee && terms.ConvenienceFee >= TrialRules.MinimumCharge ? terms.ConvenienceFee : 0m;

    private async Task<CreditBatch> RequireCreditAsync(
        Guid customerId,
        AvailabilitySlot slot,
        decimal rate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var choice = await _credits.ChooseAsync(customerId, rate, SessionStartOf(slot), now, cancellationToken);
        return choice.Batch ?? throw new DomainException(UnavailableMessage(choice.Reason!.Value), 409);
    }

    /// <summary>Opens a Razorpay order for the convenience fee of a credit booking. The credit is used at capture.</summary>
    private async Task<(decimal Amount, decimal Fee, decimal Total)> PriceCreditOrderAsync(
        User customer,
        AvailabilitySlot slot,
        bool trial,
        BookingTerms terms,
        CancellationToken cancellationToken)
    {
        if (customer.IsBlocked)
            throw new DomainException(CreditRules.BlockedMessage, 403);
        if (trial)
            throw new DomainException("A trial class can't be paid with a credit.", 409);
        var batch = await RequireCreditAsync(customer.Id, slot, RateOf(slot), _time.GetUtcNow(), cancellationToken);
        var fee = CreditFeeFor(batch, terms);
        if (fee <= 0)
            throw new DomainException("This credit booking has nothing to pay. Book it with your credit directly.", 409);
        return (0m, fee, fee);
    }

    /// <summary>
    /// At capture of a credit booking's fee: locks the customer's credits and picks the batch, or refunds the fee and
    /// fails if no credit covers the session any more (the last credit went to another booking, for example) or the
    /// customer is blocked.
    /// </summary>
    private async Task<CreditBatch> TakeCreditAtCaptureAsync(
        CheckoutIntent checkout,
        AvailabilitySlot slot,
        string paymentId,
        decimal total,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        CancellationToken cancellationToken)
    {
        var blocked = await _db.Users.AsNoTracking()
            .Where(u => u.Id == checkout.CustomerId)
            .Select(u => u.IsBlocked)
            .SingleAsync(cancellationToken);
        if (blocked)
            await RefundCheckoutAsync(checkout, paymentId, total, tx, CreditBlockedRefundedMessage, cancellationToken);

        var now = _time.GetUtcNow();
        await _credits.LockAsync(checkout.CustomerId, now, cancellationToken);
        var choice = await _credits.ChooseAsync(checkout.CustomerId, RateOf(slot), SessionStartOf(slot), now, cancellationToken);
        if (choice.Batch is null)
            await RefundCheckoutAsync(checkout, paymentId, total, tx, CreditFeeRefundedMessage, cancellationToken);
        return choice.Batch!;
    }

    private async Task<CancelQuoteResponse> QuoteCreditCancelAsync(Booking booking, string currency, CancellationToken cancellationToken)
    {
        var batch = await BatchOfAsync(booking, cancellationToken);
        var paid = booking.Payment?.Amount ?? 0m;
        var start = SessionStartOf(booking);
        var now = _time.GetUtcNow();
        var settlement = CreditRules.SettleCustomerCancel(booking, batch, paid, start, now);
        var noCreditBack = CreditRules.NoCreditBackReason(batch, settlement, now);
        return new CancelQuoteResponse(
            paid,
            0m,
            booking.ConvenienceFee,
            0m,
            paid - settlement.FeeRefund,
            settlement.FeeRefund,
            currency,
            booking.LateCancelFeeType.ToString(),
            booking.LateCancelFeeValue,
            booking.Status == BookingStatus.Upcoming ? BookingRules.FreeCancelUntil(booking, start) : null,
            true,
            noCreditBack is null,
            noCreditBack);
    }

    private async Task<BookingResponse> CancelCreditBookingAsync(Booking booking, string currency, CancellationToken cancellationToken)
    {
        var payment = booking.Payment ?? throw new DomainException("This booking has no payment record.");
        var batch = await BatchOfAsync(booking, cancellationToken);
        var start = SessionStartOf(booking);
        var now = _time.GetUtcNow();
        var settlement = CreditRules.SettleCustomerCancel(booking, batch, payment.Amount, start, now);

        BookingRules.Cancel(booking, start, now);
        PayoutPending? payout = null;
        if (settlement.InstructorShare > 0)
        {
            booking.LateCancelFee = settlement.InstructorShare;
            payout = PayoutCalculator.ForLateCancel(booking, settlement.InstructorShare, now);
            _db.PayoutsPending.Add(payout);
        }
        if (settlement.ReturnCredit)
            await _credits.ReturnAsync(booking, now, cancellationToken);
        await _notifications.CancelledAsync(booking, settlement.FeeRefund, settlement.ReturnCredit, cancellationToken);
        await RefundAndSaveAsync(booking, payment, settlement.FeeRefund, cancellationToken);

        _logger.LogInformation(
            "Customer {CustomerId} cancelled credit booking {BookingId}. Credit returned {Returned}; fee refunded {Refund}; payout {PayoutId}.",
            booking.CustomerId,
            booking.Id,
            settlement.ReturnCredit,
            settlement.FeeRefund,
            payout?.Id);
        return ToResponse(booking, payment, currency);
    }

    /// <summary>Instructor decline of a credit booking: the credit and any fee always come back.</summary>
    private async Task DeclineCreditBookingAsync(Booking booking, CancellationToken cancellationToken)
    {
        var payment = booking.Payment ?? throw new DomainException("This booking has no payment record.");
        BookingRules.Decline(booking, _time.GetUtcNow());
        await _credits.ReturnAsync(booking, _time.GetUtcNow(), cancellationToken);
        await _notifications.RefundedAsync(booking, payment.Amount, true, cancellationToken);
        await RefundAndSaveAsync(booking, payment, payment.Amount, cancellationToken);
    }

    /// <summary>With the batch's expiry rule off, a credit booking can't move past the day its credits expire.</summary>
    private async Task EnsureCreditCoversMoveAsync(Booking booking, AvailabilitySlot newSlot, CancellationToken cancellationToken)
    {
        if (booking.CreditBatchId is null)
            return;
        var batch = await BatchOfAsync(booking, cancellationToken);
        if (!CreditRules.AllowsSession(batch, SessionStartOf(newSlot)))
            throw new DomainException("Your credit expires before that session. Pick an earlier time.", 409);
    }

    private Task<CreditBatch> BatchOfAsync(Booking booking, CancellationToken cancellationToken) =>
        _db.CreditBatches.AsNoTracking().SingleAsync(b => b.Id == booking.CreditBatchId, cancellationToken);

    private static decimal RateOf(AvailabilitySlot slot) =>
        slot.Provider!.RateFor(slot.Mode) is decimal rate and > 0
            ? rate
            : throw new DomainException("This session has no price.");

    private static DateTimeOffset SessionStartOf(AvailabilitySlot slot) => MarketClock.SessionStart(slot.Date, slot.StartTime);
}

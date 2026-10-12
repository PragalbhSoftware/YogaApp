using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public readonly record struct WebhookResult(bool Booked, Guid? BookingId, Guid? CreditBatchId = null)
{
    public static WebhookResult Ignored() => new(false, null);

    public static WebhookResult ForBooking(Guid bookingId) => new(true, bookingId);

    public static WebhookResult ForPack(Guid batchId) => new(false, null, batchId);
}

public interface IBookingService
{
    Task<BookingQuoteResponse> QuoteAsync(Guid slotId, Guid? serviceId, bool trial, CancellationToken cancellationToken);

    Task<TrialStatusResponse> TrialStatusAsync(Guid providerId, CancellationToken cancellationToken);

    Task<CheckoutOrderResponse> CreateOrderAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken);

    Task<BookingResponse> CreateCreditBookingAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken);

    Task<BookingResponse> ConfirmAsync(ConfirmBookingPaymentRequest request, CancellationToken cancellationToken);

    Task<BookingResponse> ConfirmLocalAsync(LocalConfirmRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingResponse>> ListMineAsync(CancellationToken cancellationToken);

    Task<CancelQuoteResponse> QuoteCancelAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> CancelAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> RescheduleAsync(Guid bookingId, RescheduleBookingRequest request, CancellationToken cancellationToken);
}

public interface IBookingHandshake
{
    Task<IReadOnlyList<BookingResponse>> ListForInstructorAsync(string? status, CancellationToken cancellationToken);

    Task<BookingResponse> AcceptAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> DeclineAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> CompleteAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> MarkNoShowAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<ReviewResponse> CreateReviewAsync(Guid bookingId, CreateReviewRequest request, CancellationToken cancellationToken);
}

public interface IRazorpayWebhookHandler
{
    Task<WebhookResult> HandleWebhookAsync(string rawBody, string? signature, CancellationToken cancellationToken);
}

public partial class BookingService : IBookingService, IBookingHandshake, IRazorpayWebhookHandler
{
    private const string CustomerChangeForbidden = "Only the customer who booked can change this booking.";
    private const string TrialRefundedMessage = TrialRules.AlreadyUsedMessage + " We refunded this payment in full.";

    private static readonly BookingStatus[] Occupying =
        Enum.GetValues<BookingStatus>().Where(BookingRules.OccupiesSlot).ToArray();

    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IRazorpayClient _razorpay;
    private readonly IPlatformSettingsService _settings;
    private readonly CreditLedger _credits;
    private readonly CreditPurchaseService _packs;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        YogaDbContext db,
        ICurrentUser current,
        IRazorpayClient razorpay,
        IPlatformSettingsService settings,
        CreditLedger credits,
        CreditPurchaseService packs,
        ILogger<BookingService> logger)
    {
        _db = db;
        _current = current;
        _razorpay = razorpay;
        _settings = settings;
        _credits = credits;
        _packs = packs;
        _logger = logger;
    }

    public async Task<BookingQuoteResponse> QuoteAsync(Guid slotId, Guid? serviceId, bool trial, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var slot = await LoadBookableSlotAsync(slotId, cancellationToken);
        var service = await ResolveServiceAsync(slot.ProviderId, serviceId, cancellationToken);
        if (trial)
            await EnsureTrialAvailableAsync(customer.Id, service, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        var price = PriceFor(slot, settings.Terms, trial ? service : null);
        var credit = await CreditOptionAsync(customer, slot, trial, settings.Terms, cancellationToken);
        return new BookingQuoteResponse(
            slot.Id, service.Id, price.SessionAmount, price.ConvenienceFee, price.Total, settings.Currency, trial, credit);
    }

    public async Task<TrialStatusResponse> TrialStatusAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var offer = await _db.Services.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive && s.TrialEnabled && s.TrialPrice != null)
            .Select(s => s.TrialPrice)
            .FirstOrDefaultAsync(cancellationToken);
        var used = await TrialUsedAsync(customer.Id, providerId, cancellationToken);
        return new TrialStatusResponse(offer is not null, offer, used);
    }

    public async Task<CheckoutOrderResponse> CreateOrderAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var slot = await LoadBookableSlotAsync(request.SlotId, cancellationToken);
        var provider = slot.Provider!;
        var (homeAddress, landmark) = ResolveLocation(customer, slot, request);

        var service = await ResolveServiceAsync(provider.Id, request.ServiceId, cancellationToken);
        if (request.Trial && !request.UseCredit)
            await EnsureTrialAvailableAsync(customer.Id, service, cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        var terms = settings.Terms;
        var (amount, convenienceFee, total) = request.UseCredit
            ? await PriceCreditOrderAsync(customer, slot, request.Trial, terms, cancellationToken)
            : PriceFor(slot, terms, request.Trial ? service : null);
        var paise = RazorpayMoney.ToPaise(total);
        var checkoutId = Guid.NewGuid();

        var created = await _razorpay.CreateOrderAsync(
            paise,
            settings.Currency,
            checkoutId.ToString("N"),
            new Dictionary<string, string>
            {
                ["checkoutId"] = checkoutId.ToString(),
                ["slotId"] = slot.Id.ToString(),
                ["customerId"] = customer.Id.ToString()
            },
            cancellationToken);

        var checkout = new CheckoutIntent
        {
            Id = checkoutId,
            CustomerId = customer.Id,
            ProviderId = provider.Id,
            ServiceId = service.Id,
            SlotId = slot.Id,
            Mode = slot.Mode,
            Amount = amount,
            IsTrial = request.Trial,
            UsesCredit = request.UseCredit,
            ConvenienceFee = convenienceFee,
            CommissionPercent = terms.CommissionPercent,
            CancelFreeWindowHours = terms.CancelFreeWindowHours,
            LateCancelFeeType = terms.LateCancelFeeType,
            LateCancelFeeValue = terms.LateCancelFeeValue,
            Currency = settings.Currency,
            HomeAddress = slot.Mode == SessionMode.Home ? homeAddress : null,
            Landmark = slot.Mode == SessionMode.Home ? landmark : null,
            Gateway = PaymentGateways.Razorpay,
            GatewayOrderId = created.OrderId,
            Status = CheckoutStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.CheckoutIntents.Add(checkout);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Opened checkout {CheckoutId} for Razorpay order {OrderId}.", checkout.Id, checkout.GatewayOrderId);

        return new CheckoutOrderResponse(
            checkout.Id,
            _razorpay.KeyId,
            checkout.GatewayOrderId,
            paise,
            total,
            amount,
            convenienceFee,
            checkout.Currency,
            slot.Id,
            slot.Mode.ToString(),
            provider.DisplayName,
            _razorpay.SupportsLocalCapture);
    }

    public async Task<BookingResponse> ConfirmAsync(ConfirmBookingPaymentRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var orderId = Required(request.OrderId, "Order id is required.");
        var paymentId = Required(request.PaymentId, "Payment id is required.");
        var signature = Required(request.Signature, "Payment signature is required.");

        var preview = await _db.CheckoutIntents.AsNoTracking()
            .SingleOrDefaultAsync(c => c.GatewayOrderId == orderId && c.Kind == CheckoutKind.Booking, cancellationToken)
            ?? throw new DomainException("Unknown payment order.", 404);
        if (preview.CustomerId != customer.Id)
            throw new DomainException("This payment belongs to another account.", 403);

        await _razorpay.RequireCapturedCheckoutAsync(
            orderId,
            paymentId,
            signature,
            RazorpayMoney.ToPaise(preview.Amount + preview.ConvenienceFee),
            preview.Currency,
            cancellationToken);

        return await CaptureAsync(orderId, paymentId, customer.Id, amountPaise: null, currency: null, cancellationToken);
    }

    public async Task<BookingResponse> ConfirmLocalAsync(LocalConfirmRequest request, CancellationToken cancellationToken)
    {
        if (!_razorpay.SupportsLocalCapture)
            throw new DomainException("Not found.", 404);

        var orderId = Required(request.OrderId, "Order id is required.");
        var (paymentId, signature) = _razorpay.CreateLocalCapture(orderId);
        return await ConfirmAsync(
            new ConfirmBookingPaymentRequest
            {
                OrderId = orderId,
                PaymentId = paymentId,
                Signature = signature
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<BookingResponse>> ListMineAsync(CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var currency = await CurrencyAsync(cancellationToken);
        var bookings = await BookingsWithDetails(tracking: false)
            .Where(b => b.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);

        return bookings
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => ToResponse(b, b.Payment, currency))
            .ToList();
    }

    public async Task<IReadOnlyList<BookingResponse>> ListForInstructorAsync(string? status, CancellationToken cancellationToken)
    {
        var provider = await RequireInstructorAsync(cancellationToken);
        var filter = ParseStatus(status);
        var currency = await CurrencyAsync(cancellationToken);
        var query = BookingsWithDetails(tracking: false).Where(b => b.ProviderId == provider.Id);
        if (filter is BookingStatus parsed)
            query = query.Where(b => b.Status == parsed);

        var bookings = await query.ToListAsync(cancellationToken);
        return bookings
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => ToResponse(b, b.Payment, currency))
            .ToList();
    }

    public async Task<BookingResponse> AcceptAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForInstructorAsync(bookingId, cancellationToken);
        BookingRules.Accept(booking);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Instructor {ProviderId} accepted booking {BookingId}.", booking.ProviderId, booking.Id);
        return ToResponse(booking, booking.Payment, currency);
    }

    public async Task<BookingResponse> DeclineAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForInstructorAsync(bookingId, cancellationToken);
        if (booking.CreditBatchId is not null)
        {
            await DeclineCreditBookingAsync(booking, cancellationToken);
            _logger.LogInformation(
                "Instructor {ProviderId} declined credit booking {BookingId}; the credit was returned.", booking.ProviderId, booking.Id);
            return ToResponse(booking, booking.Payment, currency);
        }

        var payment = RequireRefundablePayment(booking);
        BookingRules.Decline(booking);
        await RefundAndSaveAsync(booking, payment, payment.Amount, cancellationToken);
        _logger.LogInformation(
            "Instructor {ProviderId} declined booking {BookingId} and refunded payment {PaymentId}.",
            booking.ProviderId,
            booking.Id,
            payment.GatewayPaymentId);
        return ToResponse(booking, payment, currency);
    }

    public async Task<CancelQuoteResponse> QuoteCancelAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForCustomerAsync(bookingId, cancellationToken);
        if (booking.Status is not (BookingStatus.PendingAccept or BookingStatus.Upcoming))
            throw new DomainException("Only a pending or upcoming booking can be cancelled.");
        if (booking.CreditBatchId is not null)
            return await QuoteCreditCancelAsync(booking, currency, cancellationToken);
        var paid = booking.Payment?.Amount ?? booking.Amount + booking.ConvenienceFee;
        var start = SessionStartOf(booking);
        var settlement = BookingRules.SettleCustomerCancel(booking, paid, start, DateTimeOffset.UtcNow);
        return new CancelQuoteResponse(
            paid,
            booking.Amount,
            booking.ConvenienceFee,
            settlement.LateCancelFee,
            settlement.ConvenienceFeeKept,
            settlement.Refund,
            currency,
            booking.LateCancelFeeType.ToString(),
            booking.LateCancelFeeValue,
            booking.Status == BookingStatus.Upcoming ? BookingRules.FreeCancelUntil(booking, start) : null,
            false,
            false);
    }

    public async Task<BookingResponse> CancelAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForCustomerAsync(bookingId, cancellationToken);
        if (booking.CreditBatchId is not null)
            return await CancelCreditBookingAsync(booking, currency, cancellationToken);
        var payment = RequireRefundablePayment(booking);
        var start = SessionStartOf(booking);
        var now = DateTimeOffset.UtcNow;
        var settlement = BookingRules.SettleCustomerCancel(booking, payment.Amount, start, now);

        BookingRules.Cancel(booking, start, now);
        booking.LateCancelFee = settlement.LateCancelFee > 0 ? settlement.LateCancelFee : null;

        PayoutPending? payout = null;
        if (settlement.LateCancelFee > 0)
        {
            payout = PayoutCalculator.ForLateCancel(booking, settlement.LateCancelFee);
            _db.PayoutsPending.Add(payout);
        }
        await RefundAndSaveAsync(booking, payment, settlement.Refund, cancellationToken);

        _logger.LogInformation(
            "Customer {CustomerId} cancelled booking {BookingId}. Refunded {Refund} on payment {PaymentId}; late fee {LateFee}, convenience fee kept {ConvenienceKept}, payout {PayoutId}.",
            booking.CustomerId,
            booking.Id,
            payment.RefundedAmount,
            payment.GatewayPaymentId,
            settlement.LateCancelFee,
            settlement.ConvenienceFeeKept,
            payout?.Id);
        return ToResponse(booking, payment, currency);
    }

    public async Task<BookingResponse> RescheduleAsync(
        Guid bookingId,
        RescheduleBookingRequest request,
        CancellationToken cancellationToken)
    {
        // The booking's own cancel window blocks a move, because it decides the fee.
        var customer = await RequireCustomerAsync(CustomerChangeForbidden, cancellationToken);
        if (request.SlotId == Guid.Empty)
            throw new DomainException("Slot is required.");
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var booking = await BookingsWithDetails(tracking: true)
                .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
                ?? throw new DomainException("Booking not found.", 404);
            if (booking.CustomerId != customer.Id)
                throw new DomainException("This booking belongs to another account.", 403);

            var newSlot = await _db.AvailabilitySlots
                .SingleOrDefaultAsync(s => s.Id == request.SlotId, cancellationToken)
                ?? throw new DomainException("That slot is no longer available.", 404);

            var originalStart = SessionStartOf(booking);
            BookingRules.Reschedule(booking, newSlot);
            BookingRules.EnsureOutsideLateWindow(booking, originalStart, DateTimeOffset.UtcNow);
            if (MumbaiClock.SessionStart(newSlot.Date, newSlot.EndTime) <= DateTimeOffset.UtcNow)
                throw new DomainException("That slot has already ended.", 409);
            if (await SlotIsTakenAsync(newSlot.Id, cancellationToken, booking.Id))
                throw new DomainException("That slot is no longer available.", 409);
            await EnsureCreditCoversMoveAsync(booking, newSlot, cancellationToken);

            booking.Slot = newSlot;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var currency = await CurrencyAsync(cancellationToken);
            _logger.LogInformation(
                "Customer {CustomerId} rescheduled booking {BookingId} to slot {SlotId}.",
                booking.CustomerId,
                booking.Id,
                newSlot.Id);
            return ToResponse(booking, booking.Payment, currency);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Reschedule conflict for booking {BookingId}.", bookingId);
            throw new DomainException("That slot was just booked.", 409);
        }
    }

    public Task<BookingResponse> CompleteAsync(Guid bookingId, CancellationToken cancellationToken) =>
        SettleInstructorSessionAsync(bookingId, complete: true, cancellationToken);

    public Task<BookingResponse> MarkNoShowAsync(Guid bookingId, CancellationToken cancellationToken) =>
        SettleInstructorSessionAsync(bookingId, complete: false, cancellationToken);

    private async Task<BookingResponse> SettleInstructorSessionAsync(
        Guid bookingId,
        bool complete,
        CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForInstructorAsync(bookingId, cancellationToken);
        if (complete)
            BookingRules.Complete(booking);
        else
            BookingRules.MarkNoShow(booking);

        if (booking.Payout is not null)
            throw new DomainException("This booking already has a payout.", 409);

        var payout = PayoutCalculator.ForCompletedBooking(booking);
        _db.PayoutsPending.Add(payout);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Payout save conflict for booking {BookingId}.", booking.Id);
            throw new DomainException("This booking already has a payout.", 409);
        }

        _logger.LogInformation(
            complete
                ? "Instructor {ProviderId} completed booking {BookingId}. Payout {PayoutId} net {NetAmount}."
                : "Instructor {ProviderId} marked booking {BookingId} no-show. Payout {PayoutId} net {NetAmount}.",
            booking.ProviderId,
            booking.Id,
            payout.Id,
            payout.NetAmount);
        return ToResponse(booking, booking.Payment, currency);
    }

    public async Task<ReviewResponse> CreateReviewAsync(Guid bookingId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync("Only the customer who booked can review.", cancellationToken);
        var booking = await _db.Bookings
            .Include(b => b.Review)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new DomainException("Booking not found.", 404);
        if (booking.CustomerId != customer.Id)
            throw new DomainException("This booking belongs to another account.", 403);
        if (booking.Review is not null)
            throw new DomainException("This booking already has a review.", 409);

        var review = ReviewRules.Create(booking, customer.Id, request.Rating, request.Comment);
        _db.Reviews.Add(review);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Review save conflict for booking {BookingId}.", booking.Id);
            throw new DomainException("This booking already has a review.", 409);
        }

        return new ReviewResponse(review.Id, review.BookingId, review.ProviderId, review.Rating, review.Comment, review.CreatedAt);
    }

    public async Task<WebhookResult> HandleWebhookAsync(string rawBody, string? signature, CancellationToken cancellationToken)
    {
        if (!_razorpay.VerifyWebhookSignature(rawBody, signature))
            throw new DomainException("Webhook signature is invalid.");

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
            _logger.LogWarning(ex, "Captured Razorpay payment {PaymentId} for order {OrderId} did not create a booking.", captured.PaymentId, captured.OrderId);
            return WebhookResult.Ignored();
        }
    }

    private async Task<BookingResponse> CaptureAsync(
        string orderId,
        string paymentId,
        Guid? expectedCustomerId,
        long? amountPaise,
        string? currency,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var checkout = await _db.CheckoutIntents
                .SingleOrDefaultAsync(c => c.GatewayOrderId == orderId && c.Kind == CheckoutKind.Booking, cancellationToken)
                ?? throw new DomainException("Unknown payment order.", 404);

            if (expectedCustomerId is Guid customerId && checkout.CustomerId != customerId)
                throw new DomainException("This payment belongs to another account.", 403);

            var total = checkout.Amount + checkout.ConvenienceFee;
            if (amountPaise is long paise && paise != RazorpayMoney.ToPaise(total))
                throw new DomainException("Payment amount does not match the slot price.");
            if (currency is not null && !currency.Equals(checkout.Currency, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("Payment currency does not match.");

            var replay = await LoadByPaymentIdAsync(paymentId, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.Payment.GatewayOrderId, orderId, StringComparison.Ordinal)
                    || replay.Booking.CustomerId != checkout.CustomerId)
                {
                    throw new DomainException("This payment was already used.", 409);
                }

                return ToResponse(replay.Booking, replay.Payment, checkout.Currency);
            }

            if (checkout.Status == CheckoutStatus.Completed)
                throw new DomainException("This order was already paid.", 409);
            if (checkout.Status == CheckoutStatus.Refunded)
                throw new DomainException(checkout.UsesCredit ? "This payment was refunded." : TrialRefundedMessage, 409);
            if (checkout.IsTrial && !checkout.UsesCredit
                && await TrialUsedAsync(checkout.CustomerId, checkout.ProviderId!.Value, cancellationToken))
            {
                await RefundCheckoutAsync(checkout, paymentId, total, tx, TrialRefundedMessage, cancellationToken);
            }

            var slot = await SlotsWithProvider()
                .SingleOrDefaultAsync(s => s.Id == checkout.SlotId, cancellationToken)
                ?? throw new DomainException("That slot is no longer available.", 409);

            var provider = slot.Provider ?? throw new DomainException("Instructor not found.", 404);
            if (!ProviderApproval.IsListedNow(provider) || slot.IsBlocked || !provider.Offers(slot.Mode))
                throw new DomainException("That slot is no longer available.", 409);
            EnsureAreaOpen(provider);
            if (MumbaiClock.SessionStart(slot.Date, slot.EndTime) <= DateTimeOffset.UtcNow)
                throw new DomainException("That slot has already ended.", 409);
            if (await SlotIsTakenAsync(slot.Id, cancellationToken))
                throw new DomainException("That slot was just booked.", 409);

            var service = await _db.Services.SingleOrDefaultAsync(
                s => s.Id == checkout.ServiceId && s.ProviderId == slot.ProviderId && s.IsActive,
                cancellationToken) ?? throw new DomainException("This session is no longer available.", 409);

            var batch = checkout.UsesCredit
                ? await TakeCreditAtCaptureAsync(checkout, slot, paymentId, total, tx, cancellationToken)
                : null;
            var booking = BookingRules.CreateAfterPayment(
                checkout.CustomerId,
                service,
                slot,
                batch is null ? checkout.Amount : CreditRules.InstructorGross(batch, RateOf(slot)),
                checkout.ToTerms(),
                checkout.HomeAddress,
                checkout.Landmark,
                provider.GoogleMeetLink,
                provider.StudioAddress,
                checkout.IsTrial);

            var now = DateTimeOffset.UtcNow;
            if (batch is not null)
            {
                CreditRules.Attach(booking, batch);
                _credits.Use(batch, booking.Id, now);
            }
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = total,
                Status = PaymentStatus.Pending,
                Gateway = PaymentGateways.Razorpay,
                GatewayOrderId = orderId,
                CreatedAt = now
            };
            PaymentRules.MarkPaid(payment, paymentId);

            checkout.Status = CheckoutStatus.Completed;
            checkout.BookingId = booking.Id;
            checkout.GatewayPaymentId = paymentId;
            checkout.CompletedAt = now;

            booking.Provider = provider;
            booking.Service = service;
            booking.Slot = slot;
            booking.Payment = payment;

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ToResponse(booking, payment, checkout.Currency);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Booking save conflict for Razorpay order {OrderId}.", orderId);
            await tx.DisposeAsync();
            _db.ChangeTracker.Clear();
            await RefundIfLostAsync(orderId, paymentId, cancellationToken);
            throw new DomainException("That slot was just booked.", 409);
        }
    }

    /// <summary>
    /// A clash at capture means another request saved first: the slot, this customer's trial with the instructor (a
    /// double-click, for example), or the customer's last credit. For the trial and the credit, take the same refund path
    /// as the check before saving.
    /// </summary>
    private async Task RefundIfLostAsync(string orderId, string paymentId, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var checkout = await _db.CheckoutIntents.SingleAsync(c => c.GatewayOrderId == orderId, cancellationToken);
        if (checkout.Status != CheckoutStatus.Open)
            return;
        var total = checkout.Amount + checkout.ConvenienceFee;
        if (checkout.UsesCredit)
        {
            var slot = await SlotsWithProvider().SingleAsync(s => s.Id == checkout.SlotId, cancellationToken);
            if ((await _credits.ChooseAsync(checkout.CustomerId, RateOf(slot), SessionStartOf(slot), DateTimeOffset.UtcNow, cancellationToken)).Batch is null)
                await RefundCheckoutAsync(checkout, paymentId, total, tx, CreditFeeRefundedMessage, cancellationToken);
            return;
        }
        if (checkout.IsTrial && await TrialUsedAsync(checkout.CustomerId, checkout.ProviderId!.Value, cancellationToken))
            await RefundCheckoutAsync(checkout, paymentId, total, tx, TrialRefundedMessage, cancellationToken);
    }

    private IQueryable<AvailabilitySlot> SlotsWithProvider() =>
        _db.AvailabilitySlots
            .Include(s => s.Provider!).ThenInclude(p => p.User)
            .Include(s => s.Provider!).ThenInclude(p => p.Area);

    private async Task<AvailabilitySlot> LoadBookableSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        if (slotId == Guid.Empty)
            throw new DomainException("Slot is required.");

        var slot = await SlotsWithProvider().SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken)
            ?? throw new DomainException("That slot is no longer available.", 404);

        var provider = slot.Provider ?? throw new DomainException("Instructor not found.", 404);
        if (!ProviderApproval.IsListedNow(provider))
            throw new DomainException("Instructor not found.", 404);
        EnsureAreaOpen(provider);
        if (slot.IsBlocked || !provider.Offers(slot.Mode))
            throw new DomainException("That slot is no longer available.", 409);
        if (MumbaiClock.SessionStart(slot.Date, slot.EndTime) <= DateTimeOffset.UtcNow)
            throw new DomainException("That slot has already ended.", 409);
        if (await SlotIsTakenAsync(slot.Id, cancellationToken))
            throw new DomainException("That slot is no longer available.", 409);
        return slot;
    }

    /// <summary>Uses the request's home address, else the customer's saved visit address, and checks the mode has what it needs.</summary>
    private static (string? HomeAddress, string? Landmark) ResolveLocation(User customer, AvailabilitySlot slot, CreateBookingOrderRequest request)
    {
        var provider = slot.Provider!;
        var homeAddress = Clean(request.HomeAddress, 300, "Address");
        var landmark = Clean(request.Landmark, 160, "Landmark");
        if (slot.Mode == SessionMode.Home && (string.IsNullOrWhiteSpace(homeAddress) || string.IsNullOrWhiteSpace(landmark))
            && VisitAddressRules.IsComplete(customer))
        {
            (homeAddress, landmark) = VisitAddressRules.ForBooking(customer);
        }
        BookingRules.EnsureSessionLocation(slot.Mode, homeAddress, landmark, provider.GoogleMeetLink, provider.StudioAddress);
        return (homeAddress, landmark);
    }

    private static void EnsureAreaOpen(Provider provider)
    {
        var area = provider.Area ?? throw new InvalidOperationException("Provider area was not loaded.");
        if (!area.IsActive)
            throw new DomainException(ProviderApproval.AreaClosedMessage, 409);
    }

    /// <summary>Regular sessions use the mode rate; a trial uses the service's trial price, which may be zero.</summary>
    private static (decimal SessionAmount, decimal ConvenienceFee, decimal Total) PriceFor(
        AvailabilitySlot slot,
        BookingTerms terms,
        Service? trialService = null)
    {
        var fee = terms.ConvenienceFee;
        if (trialService is not null)
        {
            var trialAmount = trialService.TrialPrice ?? throw new DomainException(TrialRules.NotOfferedMessage, 409);
            var trialTotal = decimal.Round(trialAmount + fee, 2, MidpointRounding.AwayFromZero);
            TrialRules.EnsureChargeable(trialTotal);
            return (trialAmount, fee, trialTotal);
        }

        var amount = slot.Provider!.RateFor(slot.Mode) ?? 0m;
        if (amount <= 0)
            throw new DomainException("This session has no price.");
        return (amount, fee, decimal.Round(amount + fee, 2, MidpointRounding.AwayFromZero));
    }

    private async Task EnsureTrialAvailableAsync(Guid customerId, Service service, CancellationToken cancellationToken)
    {
        if (!TrialRules.Offers(service))
            throw new DomainException(TrialRules.NotOfferedMessage, 409);
        if (await TrialUsedAsync(customerId, service.ProviderId, cancellationToken))
            throw new DomainException(TrialRules.AlreadyUsedMessage, 409);
    }

    private Task<bool> TrialUsedAsync(Guid customerId, Guid providerId, CancellationToken cancellationToken) =>
        _db.Bookings
            .AnyAsync(b => b.CustomerId == customerId && b.ProviderId == providerId && b.TrialConsumed, cancellationToken);

    /// <summary>
    /// The paid checkout can't become a booking (a second trial from another tab, or a credit that is gone). Marks the
    /// checkout refunded, refunds the whole payment, commits, and then fails the capture with <paramref name="message"/>.
    /// A replayed capture of the same order sees Refunded and stops.
    /// </summary>
    private async Task RefundCheckoutAsync(
        CheckoutIntent checkout,
        string paymentId,
        decimal total,
        IDbContextTransaction tx,
        string message,
        CancellationToken cancellationToken)
    {
        checkout.Status = CheckoutStatus.Refunded;
        checkout.GatewayPaymentId = paymentId;
        checkout.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _razorpay.RefundPaymentAsync(paymentId, RazorpayMoney.ToPaise(total), checkout.Id.ToString("N"), CancellationToken.None);
        await tx.CommitAsync(CancellationToken.None);
        _logger.LogWarning(
            "Refunded payment {PaymentId} for checkout {CheckoutId} of customer {CustomerId} with instructor {ProviderId}: {Reason}",
            paymentId,
            checkout.Id,
            checkout.CustomerId,
            checkout.ProviderId,
            message);
        throw new DomainException(message, 409);
    }

    private Task<User> RequireCustomerAsync(CancellationToken cancellationToken) =>
        RequireCustomerAsync("Only customers can book a session.", cancellationToken);

    private async Task<User> RequireCustomerAsync(string forbiddenMessage, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        if (user.Role != UserRole.Customer)
            throw new DomainException(forbiddenMessage, 403);
        return user;
    }

    private async Task<Provider> RequireInstructorAsync(CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Provider)
            .SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        if (user.Role != UserRole.Provider || user.Provider is null)
            throw new DomainException("Only the instructor can do that.", 403);
        return user.Provider;
    }

    private async Task<(Booking Booking, string Currency)> LoadForCustomerAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(CustomerChangeForbidden, cancellationToken);
        var booking = await BookingsWithDetails(tracking: true)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new DomainException("Booking not found.", 404);
        if (booking.CustomerId != customer.Id)
            throw new DomainException("This booking belongs to another account.", 403);

        return (booking, await CurrencyAsync(cancellationToken));
    }

    private async Task<(Booking Booking, string Currency)> LoadForInstructorAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var provider = await RequireInstructorAsync(cancellationToken);
        var booking = await BookingsWithDetails(tracking: true)
            .Include(b => b.Payout)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new DomainException("Booking not found.", 404);
        if (booking.ProviderId != provider.Id)
            throw new DomainException("This booking belongs to another instructor.", 403);

        var currency = await CurrencyAsync(cancellationToken);
        return (booking, currency);
    }

    private IQueryable<Booking> BookingsWithDetails(bool tracking)
    {
        var query = tracking ? _db.Bookings : _db.Bookings.AsNoTracking();
        return query
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Slot)
            .Include(b => b.Payment)
            .Include(b => b.Review);
    }

    private async Task<string> CurrencyAsync(CancellationToken cancellationToken) =>
        (await _settings.GetAsync(cancellationToken)).Currency;

    private static BookingStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        if (!Enum.TryParse<BookingStatus>(status.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new DomainException("Unknown booking status.");
        return parsed;
    }

    private async Task<Service> ResolveServiceAsync(Guid providerId, Guid? serviceId, CancellationToken cancellationToken)
    {
        var services = await _db.Services.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .ToListAsync(cancellationToken);

        if (serviceId is Guid requested && requested != Guid.Empty)
        {
            return services.SingleOrDefault(s => s.Id == requested)
                ?? throw new DomainException("Choose a service this instructor offers.");
        }

        if (services.Count == 1)
            return services[0];

        throw new DomainException(services.Count == 0
            ? "This instructor has no bookable service."
            : "Choose a service.");
    }

    private Task<bool> SlotIsTakenAsync(Guid slotId, CancellationToken cancellationToken, Guid? exceptBookingId = null)
    {
        var query = _db.Bookings.Where(b => b.SlotId == slotId && Occupying.Contains(b.Status));
        if (exceptBookingId is Guid bookingId)
            query = query.Where(b => b.Id != bookingId);
        return query.AnyAsync(cancellationToken);
    }

    private static Payment RequireRefundablePayment(Booking booking)
    {
        var payment = booking.Payment ?? throw new DomainException("This booking has no payment to refund.");
        if (string.IsNullOrWhiteSpace(payment.GatewayPaymentId))
            throw new DomainException("This payment cannot be refunded.");
        return payment;
    }

    private Task RefundAndSaveAsync(
        Booking booking,
        Payment payment,
        decimal amount,
        CancellationToken cancellationToken) =>
        PaymentRefunds.RefundAndSaveAsync(_db, _razorpay, _logger, booking, payment, amount, cancellationToken);

    private static DateTimeOffset SessionStartOf(Booking booking)
    {
        var slot = booking.Slot ?? throw new InvalidOperationException("Slot was not loaded.");
        return MumbaiClock.SessionStart(slot.Date, slot.StartTime);
    }

    private async Task<LoadedBooking?> LoadByPaymentIdAsync(string paymentId, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking!).ThenInclude(b => b.Provider)
            .Include(p => p.Booking!).ThenInclude(b => b.Service)
            .Include(p => p.Booking!).ThenInclude(b => b.Slot)
            .Include(p => p.Booking!).ThenInclude(b => b.Review)
            .SingleOrDefaultAsync(p => p.GatewayPaymentId == paymentId, cancellationToken);

        if (payment?.Booking is null)
            return null;
        return new LoadedBooking(payment.Booking, payment);
    }

    private static BookingResponse ToResponse(Booking booking, Payment? payment, string currency)
    {
        var slot = booking.Slot ?? throw new InvalidOperationException("Slot was not loaded.");
        var provider = booking.Provider ?? throw new InvalidOperationException("Provider was not loaded.");
        var service = booking.Service ?? throw new InvalidOperationException("Service was not loaded.");
        return new BookingResponse(
            booking.Id,
            provider.Id,
            provider.DisplayName,
            service.Id,
            service.Title,
            slot.Id,
            booking.Mode.ToString(),
            booking.Status.ToString(),
            booking.Amount,
            booking.ConvenienceFee,
            currency,
            slot.Date,
            slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            booking.HomeAddress,
            booking.Landmark,
            booking.MeetLinkSnapshot,
            booking.StudioAddressSnapshot,
            payment?.Status.ToString() ?? "",
            payment?.GatewayOrderId,
            payment?.GatewayPaymentId,
            booking.CreatedAt,
            booking.Review is not null,
            payment?.RefundedAmount ?? 0m,
            booking.LateCancelFee,
            booking.CancelledBy?.ToString(),
            booking.CancelReason,
            booking.IsTrial,
            booking.CreditBatchId is not null);
    }

    private static string Required(string? value, string message)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
            throw new DomainException(message);
        return trimmed;
    }

    private static string? Clean(string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new DomainException($"{label} must be {max} characters or less.");
        return trimmed;
    }

    private sealed record LoadedBooking(Booking Booking, Payment Payment);
}

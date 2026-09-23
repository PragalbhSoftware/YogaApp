using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public readonly record struct WebhookResult(bool Booked, Guid? BookingId)
{
    public static WebhookResult Ignored() => new(false, null);

    public static WebhookResult ForBooking(Guid bookingId) => new(true, bookingId);
}

public interface IBookingService
{
    Task<CheckoutOrderResponse> CreateOrderAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken);

    Task<BookingResponse> ConfirmAsync(ConfirmBookingPaymentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingResponse>> ListMineAsync(CancellationToken cancellationToken);

    Task<BookingResponse> CancelAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> RescheduleAsync(Guid bookingId, RescheduleBookingRequest request, CancellationToken cancellationToken);
}

public interface IBookingHandshake
{
    Task<IReadOnlyList<BookingResponse>> ListForInstructorAsync(string? status, CancellationToken cancellationToken);

    Task<BookingResponse> AcceptAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> DeclineAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<BookingResponse> CompleteAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<ReviewResponse> CreateReviewAsync(Guid bookingId, CreateReviewRequest request, CancellationToken cancellationToken);
}

public interface IRazorpayWebhookHandler
{
    Task<WebhookResult> HandleWebhookAsync(string rawBody, string? signature, CancellationToken cancellationToken);
}

public class BookingService : IBookingService, IBookingHandshake, IRazorpayWebhookHandler
{
    private const string CustomerChangeForbidden = "Only the customer who booked can change this booking.";

    private static readonly BookingStatus[] Occupying =
        Enum.GetValues<BookingStatus>().Where(BookingRules.OccupiesSlot).ToArray();

    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IRazorpayClient _razorpay;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        YogaDbContext db,
        ICurrentUser current,
        IRazorpayClient razorpay,
        ILogger<BookingService> logger)
    {
        _db = db;
        _current = current;
        _razorpay = razorpay;
        _logger = logger;
    }

    public async Task<CheckoutOrderResponse> CreateOrderAsync(CreateBookingOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        if (request.SlotId == Guid.Empty)
            throw new DomainException("Slot is required.");

        var slot = await _db.AvailabilitySlots
            .Include(s => s.Provider)
            .SingleOrDefaultAsync(s => s.Id == request.SlotId, cancellationToken)
            ?? throw new DomainException("That slot is no longer available.", 404);

        var provider = slot.Provider ?? throw new DomainException("Instructor not found.", 404);
        if (provider.Status != ProviderStatus.Verified)
            throw new DomainException("Instructor not found.", 404);
        if (slot.IsBlocked || !provider.Offers(slot.Mode))
            throw new DomainException("That slot is no longer available.", 409);
        if (MumbaiClock.SessionStart(slot.Date, slot.EndTime) <= DateTimeOffset.UtcNow)
            throw new DomainException("That slot has already ended.", 409);
        if (await SlotIsTakenAsync(slot.Id, cancellationToken))
            throw new DomainException("That slot is no longer available.", 409);

        var amount = provider.RateFor(slot.Mode) ?? throw new DomainException("This session has no price.");
        if (amount <= 0)
            throw new DomainException("This session has no price.");

        var homeAddress = Clean(request.HomeAddress, 300, "Address");
        var landmark = Clean(request.Landmark, 160, "Landmark");
        BookingRules.EnsureSessionLocation(slot.Mode, homeAddress, landmark, provider.GoogleMeetLink, provider.StudioAddress);

        var service = await ResolveServiceAsync(provider.Id, request.ServiceId, cancellationToken);
        var policy = await _db.Policies.AsNoTracking().SingleAsync(cancellationToken);
        var paise = RazorpayMoney.ToPaise(amount);
        var checkoutId = Guid.NewGuid();

        var created = await _razorpay.CreateOrderAsync(
            paise,
            policy.Currency,
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
            Currency = policy.Currency,
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
            amount,
            checkout.Currency,
            slot.Id,
            slot.Mode.ToString(),
            provider.DisplayName);
    }

    public async Task<BookingResponse> ConfirmAsync(ConfirmBookingPaymentRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var orderId = Required(request.OrderId, "Order id is required.");
        var paymentId = Required(request.PaymentId, "Payment id is required.");
        var signature = Required(request.Signature, "Payment signature is required.");

        var preview = await _db.CheckoutIntents.AsNoTracking()
            .SingleOrDefaultAsync(c => c.GatewayOrderId == orderId, cancellationToken)
            ?? throw new DomainException("Unknown payment order.", 404);
        if (preview.CustomerId != customer.Id)
            throw new DomainException("This payment belongs to another account.", 403);

        await _razorpay.RequireCapturedCheckoutAsync(
            orderId,
            paymentId,
            signature,
            RazorpayMoney.ToPaise(preview.Amount),
            preview.Currency,
            cancellationToken);

        return await CaptureAsync(orderId, paymentId, customer.Id, amountPaise: null, currency: null, cancellationToken);
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
        var payment = RequireRefundablePayment(booking);
        BookingRules.Decline(booking);
        await RefundCapturedPaymentAsync(booking, payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Instructor {ProviderId} declined booking {BookingId} and refunded payment {PaymentId}.",
            booking.ProviderId,
            booking.Id,
            payment.GatewayPaymentId);
        return ToResponse(booking, payment, currency);
    }

    public async Task<BookingResponse> CancelAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForCustomerAsync(bookingId, cancellationToken);
        var slot = booking.Slot ?? throw new InvalidOperationException("Slot was not loaded.");
        var payment = RequireRefundablePayment(booking);
        // Free-window hours and LateCancelFeePercent stay TBD on MarketplacePolicy. A cancel before the session starts refunds the full capture.
        BookingRules.Cancel(booking, MumbaiClock.SessionStart(slot.Date, slot.StartTime), DateTimeOffset.UtcNow);
        await RefundCapturedPaymentAsync(booking, payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Customer {CustomerId} cancelled booking {BookingId} and refunded payment {PaymentId}.",
            booking.CustomerId,
            booking.Id,
            payment.GatewayPaymentId);
        return ToResponse(booking, payment, currency);
    }

    public async Task<BookingResponse> RescheduleAsync(
        Guid bookingId,
        RescheduleBookingRequest request,
        CancellationToken cancellationToken)
    {
        // RescheduleFreeWindowHours is a TBD default and does not block a move onto another open slot.
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

            BookingRules.Reschedule(booking, newSlot);
            if (MumbaiClock.SessionStart(newSlot.Date, newSlot.EndTime) <= DateTimeOffset.UtcNow)
                throw new DomainException("That slot has already ended.", 409);
            if (await SlotIsTakenAsync(newSlot.Id, cancellationToken, booking.Id))
                throw new DomainException("That slot is no longer available.", 409);

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

    public async Task<BookingResponse> CompleteAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var (booking, currency) = await LoadForInstructorAsync(bookingId, cancellationToken);
        BookingRules.Complete(booking);
        if (booking.Payout is not null)
            throw new DomainException("This booking already has a payout.", 409);

        var feePercent = await _db.Policies.AsNoTracking()
            .Select(p => p.PlatformFeePercent)
            .SingleAsync(cancellationToken);
        var payout = PayoutCalculator.ForCompletedBooking(booking, feePercent);
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
            "Instructor {ProviderId} completed booking {BookingId}. Payout {PayoutId} net {NetAmount}.",
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
            var checkout = await _db.CheckoutIntents.SingleOrDefaultAsync(c => c.GatewayOrderId == orderId, cancellationToken)
                ?? throw new DomainException("Unknown payment order.", 404);

            if (expectedCustomerId is Guid customerId && checkout.CustomerId != customerId)
                throw new DomainException("This payment belongs to another account.", 403);

            if (amountPaise is long paise && paise != RazorpayMoney.ToPaise(checkout.Amount))
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

            var slot = await _db.AvailabilitySlots
                .Include(s => s.Provider)
                .SingleOrDefaultAsync(s => s.Id == checkout.SlotId, cancellationToken)
                ?? throw new DomainException("That slot is no longer available.", 409);

            var provider = slot.Provider ?? throw new DomainException("Instructor not found.", 404);
            if (provider.Status != ProviderStatus.Verified || slot.IsBlocked || !provider.Offers(slot.Mode))
                throw new DomainException("That slot is no longer available.", 409);
            if (MumbaiClock.SessionStart(slot.Date, slot.EndTime) <= DateTimeOffset.UtcNow)
                throw new DomainException("That slot has already ended.", 409);
            if (await SlotIsTakenAsync(slot.Id, cancellationToken))
                throw new DomainException("That slot was just booked.", 409);

            var service = await _db.Services.SingleOrDefaultAsync(
                s => s.Id == checkout.ServiceId && s.ProviderId == slot.ProviderId && s.IsActive,
                cancellationToken) ?? throw new DomainException("This session is no longer available.", 409);

            var booking = BookingRules.CreateAfterPayment(
                checkout.CustomerId,
                service,
                slot,
                checkout.Amount,
                checkout.HomeAddress,
                checkout.Landmark,
                provider.GoogleMeetLink,
                provider.StudioAddress);

            var now = DateTimeOffset.UtcNow;
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = checkout.Amount,
                Status = PaymentStatus.Pending,
                Gateway = PaymentGateways.Razorpay,
                GatewayOrderId = orderId,
                CreatedAt = now
            };
            PaymentRules.MarkPaid(payment, paymentId);

            checkout.Status = CheckoutStatus.Completed;
            checkout.BookingId = booking.Id;
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
            throw new DomainException("That slot was just booked.", 409);
        }
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

    private Task<string> CurrencyAsync(CancellationToken cancellationToken) =>
        _db.Policies.AsNoTracking().Select(p => p.Currency).SingleAsync(cancellationToken);

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

    private async Task RefundCapturedPaymentAsync(Booking booking, Payment payment, CancellationToken cancellationToken)
    {
        PaymentRules.MarkRefunded(payment);
        await _razorpay.RefundPaymentAsync(
            payment.GatewayPaymentId!,
            RazorpayMoney.ToPaise(payment.Amount),
            booking.Id.ToString("N"),
            cancellationToken);
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
            booking.Review is not null);
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

using System.Data;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// Buying credit packs. A pack checkout is a <see cref="CheckoutIntent"/> of kind Pack. Credits exist only after the
/// server captures the payment, from the checkout confirm or from the Razorpay webhook, whichever arrives first;
/// the other finds the batch already made. The pack rules and current credit settings are copied onto the batch.
/// </summary>
public class CreditPurchaseService
{
    private const string BlockedRefundedMessage = CreditRules.BlockedMessage + " We refunded this payment in full.";

    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IRazorpayClient _razorpay;
    private readonly IPlatformSettingsService _settings;
    private readonly CreditLedger _credits;
    private readonly ILogger<CreditPurchaseService> _logger;

    public CreditPurchaseService(
        YogaDbContext db,
        ICurrentUser current,
        IRazorpayClient razorpay,
        IPlatformSettingsService settings,
        CreditLedger credits,
        ILogger<CreditPurchaseService> logger)
    {
        _db = db;
        _current = current;
        _razorpay = razorpay;
        _settings = settings;
        _credits = credits;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PackResponse>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var currency = (await _settings.GetAsync(cancellationToken)).Currency;
        var packs = await _db.CreditPacks.AsNoTracking().Where(p => p.IsActive).ToListAsync(cancellationToken);
        return packs
            .OrderBy(p => p.Price)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => new PackResponse(
                p.Id,
                p.Name,
                p.Sessions,
                p.Price,
                decimal.Round(p.Price / p.Sessions, 2, MidpointRounding.AwayFromZero),
                p.ValidityDays,
                p.PriceCap,
                currency))
            .ToList();
    }

    public async Task<MyCreditsResponse> MineAsync(CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var batches = await _credits.UsableAsync(customer.Id, DateTimeOffset.UtcNow, cancellationToken);
        return new MyCreditsResponse(batches.Sum(b => b.Remaining), batches.Select(ToResponse).ToList());
    }

    public async Task<PackOrderResponse> CreateOrderAsync(Guid packId, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        if (customer.IsBlocked)
            throw new DomainException(CreditRules.BlockedMessage, 403);
        var pack = await _db.CreditPacks.AsNoTracking().SingleOrDefaultAsync(p => p.Id == packId && p.IsActive, cancellationToken)
            ?? throw new DomainException("This pack is no longer available.", 404);
        var currency = (await _settings.GetAsync(cancellationToken)).Currency;
        var paise = RazorpayMoney.ToPaise(pack.Price);
        var checkoutId = Guid.NewGuid();

        var created = await _razorpay.CreateOrderAsync(
            paise,
            currency,
            checkoutId.ToString("N"),
            new Dictionary<string, string>
            {
                ["checkoutId"] = checkoutId.ToString(),
                ["packId"] = pack.Id.ToString(),
                ["customerId"] = customer.Id.ToString()
            },
            cancellationToken);

        _db.CheckoutIntents.Add(new CheckoutIntent
        {
            Id = checkoutId,
            Kind = CheckoutKind.Pack,
            CustomerId = customer.Id,
            PackId = pack.Id,
            Amount = pack.Price,
            Currency = currency,
            Gateway = PaymentGateways.Razorpay,
            GatewayOrderId = created.OrderId,
            Status = CheckoutStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Opened pack checkout {CheckoutId} for pack {PackId}, Razorpay order {OrderId}.", checkoutId, pack.Id, created.OrderId);

        return new PackOrderResponse(
            checkoutId, _razorpay.KeyId, created.OrderId, paise, pack.Price, currency, pack.Id, pack.Name, _razorpay.SupportsLocalCapture);
    }

    public async Task<PackOrderStatusResponse> ConfirmAsync(ConfirmBookingPaymentRequest request, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var orderId = Required(request.OrderId, "Order id is required.");
        var paymentId = Required(request.PaymentId, "Payment id is required.");
        var signature = Required(request.Signature, "Payment signature is required.");
        var checkout = await LoadPackCheckoutAsync(orderId, customer.Id, cancellationToken);

        await _razorpay.RequireCapturedCheckoutAsync(
            orderId, paymentId, signature, RazorpayMoney.ToPaise(checkout.Amount), checkout.Currency, cancellationToken);
        return await CaptureAsync(orderId, paymentId, customer.Id, null, null, cancellationToken);
    }

    public async Task<PackOrderStatusResponse> ConfirmLocalAsync(LocalConfirmRequest request, CancellationToken cancellationToken)
    {
        if (!_razorpay.SupportsLocalCapture)
            throw new DomainException("Not found.", 404);
        var orderId = Required(request.OrderId, "Order id is required.");
        var (paymentId, signature) = _razorpay.CreateLocalCapture(orderId);
        return await ConfirmAsync(
            new ConfirmBookingPaymentRequest { OrderId = orderId, PaymentId = paymentId, Signature = signature },
            cancellationToken);
    }

    public async Task<PackOrderStatusResponse> StatusAsync(string orderId, CancellationToken cancellationToken)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var checkout = await LoadPackCheckoutAsync(orderId, customer.Id, cancellationToken);
        return await StatusOfAsync(checkout, cancellationToken);
    }

    internal async Task<WebhookResult> HandleCapturedWebhookAsync(CapturedRazorpayPayment captured, CancellationToken cancellationToken)
    {
        try
        {
            var status = await CaptureAsync(captured.OrderId, captured.PaymentId, null, captured.AmountPaise, captured.Currency, cancellationToken);
            return status.Batch is { } batch ? WebhookResult.ForPack(batch.Id) : WebhookResult.Ignored();
        }
        catch (DomainException ex) when (ex.StatusCode is 404 or 409)
        {
            _logger.LogWarning(ex, "Captured Razorpay payment {PaymentId} for pack order {OrderId} did not add credits.", captured.PaymentId, captured.OrderId);
            return WebhookResult.Ignored();
        }
    }

    private async Task<PackOrderStatusResponse> CaptureAsync(
        string orderId,
        string paymentId,
        Guid? expectedCustomerId,
        long? amountPaise,
        string? currency,
        CancellationToken cancellationToken)
    {
        var customerId = await _db.CheckoutIntents.AsNoTracking()
            .Where(c => c.GatewayOrderId == orderId && c.Kind == CheckoutKind.Pack)
            .Select(c => (Guid?)c.CustomerId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Unknown payment order.", 404);
        await _credits.EnsureAccountAsync(customerId, cancellationToken);

        try
        {
            return await CaptureInTransactionAsync(orderId, paymentId, expectedCustomerId, amountPaise, currency, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // A confirm and a webhook for the same payment raced; the loser finds the batch the winner made.
            _logger.LogWarning(ex, "Pack capture conflict for Razorpay order {OrderId}.", orderId);
            _db.ChangeTracker.Clear();
            var checkout = await _db.CheckoutIntents.AsNoTracking().SingleAsync(c => c.GatewayOrderId == orderId, cancellationToken);
            if (checkout.Status == CheckoutStatus.Completed && checkout.GatewayPaymentId == paymentId)
                return await StatusOfAsync(checkout, cancellationToken);
            throw new DomainException("This payment could not be added. Try again.", 409);
        }
    }

    private async Task<PackOrderStatusResponse> CaptureInTransactionAsync(
        string orderId,
        string paymentId,
        Guid? expectedCustomerId,
        long? amountPaise,
        string? currency,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var checkout = await _db.CheckoutIntents.SingleAsync(c => c.GatewayOrderId == orderId, cancellationToken);
        if (expectedCustomerId is Guid expected && checkout.CustomerId != expected)
            throw new DomainException("This payment belongs to another account.", 403);
        if (amountPaise is long paise && paise != RazorpayMoney.ToPaise(checkout.Amount))
            throw new PaymentMismatchException("Payment amount does not match the pack price.");
        if (currency is not null && !currency.Equals(checkout.Currency, StringComparison.OrdinalIgnoreCase))
            throw new PaymentMismatchException("Payment currency does not match.");

        // A failed attempt can still be followed by a capture on the same order.
        if (checkout.Status is not (CheckoutStatus.Open or CheckoutStatus.Failed))
        {
            if (checkout.GatewayPaymentId != paymentId)
                throw new DomainException("This order was already paid.", 409);
            return await StatusOfAsync(checkout, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var blocked = await _db.Users.AsNoTracking().Where(u => u.Id == checkout.CustomerId).Select(u => u.IsBlocked).SingleAsync(cancellationToken);
        if (blocked)
        {
            checkout.Status = CheckoutStatus.Refunded;
            checkout.GatewayPaymentId = paymentId;
            checkout.CompletedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            await _razorpay.RefundPaymentAsync(paymentId, RazorpayMoney.ToPaise(checkout.Amount), checkout.Id.ToString("N"), CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);
            _logger.LogWarning("Refunded pack payment {PaymentId}: customer {CustomerId} is blocked.", paymentId, checkout.CustomerId);
            throw new DomainException(BlockedRefundedMessage, 409);
        }

        var pack = await _db.CreditPacks.AsNoTracking().SingleAsync(p => p.Id == checkout.PackId, cancellationToken);
        var terms = (await _settings.GetAsync(cancellationToken)).Credits;
        var batch = new CreditBatch
        {
            Id = Guid.NewGuid(),
            CustomerId = checkout.CustomerId,
            PackId = pack.Id,
            CheckoutIntentId = checkout.Id,
            PackName = pack.Name,
            Sessions = pack.Sessions,
            Price = checkout.Amount,
            ValidityDays = pack.ValidityDays,
            PriceCap = pack.PriceCap,
            Currency = checkout.Currency,
            ChargeConvenienceFee = terms.ChargeConvenienceFee,
            PayBasis = terms.PayBasis,
            LateCancel = terms.LateCancel,
            BookingStandsAfterExpiry = terms.BookingStandsAfterExpiry,
            RefundsEnabled = terms.RefundsEnabled,
            ReferenceSessionPrice = terms.ReferenceSessionPrice,
            RefundFee = terms.RefundFee,
            GatewayPaymentId = paymentId,
            PurchasedAt = now,
            ExpiresAt = CreditRules.ExpiresAt(now, pack.ValidityDays),
            Remaining = pack.Sessions
        };
        await _credits.LockAsync(checkout.CustomerId, now, cancellationToken);
        _credits.AddPurchase(batch);
        checkout.Status = CheckoutStatus.Completed;
        checkout.GatewayPaymentId = paymentId;
        checkout.CompletedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Customer {CustomerId} bought pack {PackId}: batch {BatchId} with {Sessions} credits, payment {PaymentId}.",
            batch.CustomerId, pack.Id, batch.Id, batch.Sessions, paymentId);
        return new PackOrderStatusResponse(orderId, checkout.Status.ToString(), ToResponse(batch));
    }

    private async Task<PackOrderStatusResponse> StatusOfAsync(CheckoutIntent checkout, CancellationToken cancellationToken)
    {
        var batch = await _db.CreditBatches.AsNoTracking().SingleOrDefaultAsync(b => b.CheckoutIntentId == checkout.Id, cancellationToken);
        return new PackOrderStatusResponse(checkout.GatewayOrderId, checkout.Status.ToString(), batch is null ? null : ToResponse(batch));
    }

    private async Task<CheckoutIntent> LoadPackCheckoutAsync(string orderId, Guid customerId, CancellationToken cancellationToken)
    {
        var checkout = await _db.CheckoutIntents.AsNoTracking()
            .SingleOrDefaultAsync(c => c.GatewayOrderId == orderId && c.Kind == CheckoutKind.Pack, cancellationToken)
            ?? throw new DomainException("Unknown payment order.", 404);
        if (checkout.CustomerId != customerId)
            throw new DomainException("This payment belongs to another account.", 403);
        return checkout;
    }

    private async Task<User> RequireCustomerAsync(CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken)
            ?? throw new DomainException("Sign in required.", 401);
        if (user.Role != UserRole.Customer)
            throw new DomainException("Only customers can buy packs.", 403);
        return user;
    }

    internal static CreditBatchResponse ToResponse(CreditBatch batch) => new(
        batch.Id,
        batch.PackName,
        batch.Sessions,
        batch.Remaining,
        batch.PriceCap,
        batch.PurchasedAt,
        batch.ExpiresAt,
        CreditRules.LastDay(batch));

    private static string Required(string? value, string message)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
            throw new DomainException(message);
        return trimmed;
    }
}

using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminCreditService
{
    Task<IReadOnlyList<AdminPackResponse>> ListPacksAsync(CancellationToken cancellationToken);

    Task<AdminPackResponse> CreatePackAsync(SavePackRequest request, CancellationToken cancellationToken);

    Task<AdminPackResponse> UpdatePackAsync(Guid id, SavePackRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminCreditBatchResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AdminCreditBatchResponse> RefundAsync(Guid batchId, CancellationToken cancellationToken);
}

/// <summary>
/// Admin packs and partial refunds. Packs are never deleted. A refund uses the rules saved on the batch: unused credits
/// at the reference session price, minus the refund fee, capped at what was paid. Credits held by upcoming bookings
/// already count as used, so those bookings stay.
/// </summary>
public class AdminCreditService : IAdminCreditService
{
    private const string StaleMessage = "Someone else changed this pack first. Reload to see the latest values.";

    private readonly YogaDbContext _db;
    private readonly IRazorpayClient _razorpay;
    private readonly CreditLedger _credits;
    private readonly ICurrentUser _current;
    private readonly ILogger<AdminCreditService> _logger;

    public AdminCreditService(
        YogaDbContext db,
        IRazorpayClient razorpay,
        CreditLedger credits,
        ICurrentUser current,
        ILogger<AdminCreditService> logger)
    {
        _db = db;
        _razorpay = razorpay;
        _credits = credits;
        _current = current;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminPackResponse>> ListPacksAsync(CancellationToken cancellationToken)
    {
        var packs = await _db.CreditPacks.AsNoTracking().ToListAsync(cancellationToken);
        var sold = await _db.CreditBatches.AsNoTracking()
            .GroupBy(b => b.PackId)
            .Select(g => new { PackId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PackId, g => g.Count, cancellationToken);
        return packs
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Price)
            .Select(p => ToResponse(p, sold.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<AdminPackResponse> CreatePackAsync(SavePackRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? "").Trim();
        var sessions = request.Sessions ?? throw new DomainException("Sessions are required.");
        var price = request.Price ?? throw new DomainException("Price is required.");
        var validity = request.ValidityDays ?? throw new DomainException("Validity is required.");
        var cap = request.PriceCap ?? throw new DomainException("Price cap is required.");
        CreditPackRules.Validate(name, sessions, price, validity, cap);

        var pack = new CreditPack
        {
            Id = Guid.NewGuid(),
            Name = name,
            Sessions = sessions,
            Price = price,
            ValidityDays = validity,
            PriceCap = cap,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.CreditPacks.Add(pack);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Admin {AdminId} created pack {PackId} ({Name}).", _current.UserId, pack.Id, pack.Name);
        return ToResponse(pack, 0);
    }

    public async Task<AdminPackResponse> UpdatePackAsync(Guid id, SavePackRequest request, CancellationToken cancellationToken)
    {
        if (request.Version is not int expected)
            throw new DomainException("Send the pack version you edited.");
        var pack = await _db.CreditPacks.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new DomainException("Pack not found.", 404);
        if (pack.Version != expected)
            throw new DomainException(StaleMessage, 409);

        var name = request.Name is null ? pack.Name : request.Name.Trim();
        var sessions = request.Sessions ?? pack.Sessions;
        var price = request.Price ?? pack.Price;
        var validity = request.ValidityDays ?? pack.ValidityDays;
        var cap = request.PriceCap ?? pack.PriceCap;
        CreditPackRules.Validate(name, sessions, price, validity, cap);

        pack.Name = name;
        pack.Sessions = sessions;
        pack.Price = price;
        pack.ValidityDays = validity;
        pack.PriceCap = cap;
        pack.IsActive = request.IsActive ?? pack.IsActive;
        pack.Version = expected + 1;
        pack.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(StaleMessage, 409);
        }

        _logger.LogInformation("Admin {AdminId} updated pack {PackId} to version {Version}; active {Active}.", _current.UserId, pack.Id, pack.Version, pack.IsActive);
        var sold = await _db.CreditBatches.CountAsync(b => b.PackId == pack.Id, cancellationToken);
        return ToResponse(pack, sold);
    }

    public async Task<IReadOnlyList<AdminCreditBatchResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            throw new DomainException("User not found.", 404);
        var batches = await _db.CreditBatches.AsNoTracking().Where(b => b.CustomerId == userId).ToListAsync(cancellationToken);
        var used = await UsedByBatchAsync(batches.Select(b => b.Id).ToList(), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return batches
            .OrderByDescending(b => b.PurchasedAt)
            .Select(b => ToResponse(b, used.GetValueOrDefault(b.Id), now))
            .ToList();
    }

    public async Task<AdminCreditBatchResponse> RefundAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var customerId = await _db.CreditBatches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => (Guid?)b.CustomerId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Credit pack purchase not found.", 404);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        // Lock before reading the batch, so a booking that uses a credit meanwhile makes this save fail instead of refunding it.
        await _credits.LockAsync(customerId, now, cancellationToken);
        var batch = await _db.CreditBatches.SingleAsync(b => b.Id == batchId, cancellationToken);
        CreditRules.EnsureRefundable(batch, now);
        var amount = CreditRules.RefundFor(batch);
        var unused = batch.Remaining;
        _credits.Refund(batch, amount, now);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Pack refund for batch {BatchId} lost a race.", batch.Id);
            throw new DomainException("This pack was just changed. Refresh and try again.", 409);
        }

        // Once Razorpay is called the request must not be cancelled before the commit records it.
        if (amount > 0)
            batch.GatewayRefundId = await _razorpay.RefundPaymentAsync(
                batch.GatewayPaymentId, RazorpayMoney.ToPaise(amount), batch.Id.ToString("N"), CancellationToken.None);
        try
        {
            if (batch.GatewayRefundId is not null)
                await _db.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Refunded {Amount} on pack payment {PaymentId} for batch {BatchId} but the commit failed. Reconcile with Razorpay.",
                amount, batch.GatewayPaymentId, batch.Id);
            throw;
        }

        _logger.LogInformation(
            "Admin {AdminId} refunded {Amount} for {Unused} unused credits on batch {BatchId}, payment {PaymentId}.",
            _current.UserId, amount, unused, batch.Id, batch.GatewayPaymentId);
        var used = await UsedByBatchAsync([batch.Id], cancellationToken);
        return ToResponse(batch, used.GetValueOrDefault(batch.Id), now);
    }

    /// <summary>Credits spent on bookings: uses minus returns.</summary>
    private async Task<Dictionary<Guid, int>> UsedByBatchAsync(List<Guid> batchIds, CancellationToken cancellationToken)
    {
        var rows = await _db.CreditLedger.AsNoTracking()
            .Where(e => batchIds.Contains(e.BatchId) && (e.Kind == CreditEntryKind.Use || e.Kind == CreditEntryKind.Return))
            .Select(e => new { e.BatchId, e.Kind })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.BatchId)
            .ToDictionary(g => g.Key, g => g.Count(r => r.Kind == CreditEntryKind.Use) - g.Count(r => r.Kind == CreditEntryKind.Return));
    }

    private static AdminPackResponse ToResponse(CreditPack pack, int sold) => new(
        pack.Id,
        pack.Name,
        pack.Sessions,
        pack.Price,
        pack.ValidityDays,
        pack.PriceCap,
        pack.IsActive,
        pack.Version,
        sold,
        pack.CreatedAt,
        pack.UpdatedAt);

    private static AdminCreditBatchResponse ToResponse(CreditBatch batch, int used, DateTimeOffset now)
    {
        var status = batch.RefundedAt is not null ? "Refunded"
            : batch.ExpiresAt <= now ? "Expired"
            : batch.Remaining == 0 ? "UsedUp"
            : "Active";
        var refundable = status == "Active" && batch.RefundsEnabled;
        return new AdminCreditBatchResponse(
            batch.Id,
            batch.PackId,
            batch.PackName,
            batch.Sessions,
            batch.Remaining,
            used,
            batch.Price,
            batch.PriceCap,
            batch.Currency,
            batch.PurchasedAt,
            batch.ExpiresAt,
            CreditRules.LastDay(batch),
            status,
            batch.RefundsEnabled,
            batch.ReferenceSessionPrice,
            batch.RefundFee,
            refundable ? CreditRules.RefundFor(batch) : 0m,
            batch.RefundedAmount,
            batch.RefundedAt,
            batch.GatewayPaymentId,
            batch.PayBasis.ToString(),
            batch.LateCancel.ToString(),
            batch.BookingStandsAfterExpiry,
            batch.ChargeConvenienceFee);
    }
}

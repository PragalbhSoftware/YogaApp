using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// Moves credits. Every change goes through <see cref="LockAsync"/>, which bumps the customer's
/// <see cref="CreditAccount.Version"/>; two saves racing on the same customer can't both win. Nothing here saves;
/// callers save the ledger rows together with the booking or refund they belong to.
/// </summary>
public class CreditLedger
{
    private readonly YogaDbContext _db;
    private readonly TimeProvider _time;

    public CreditLedger(YogaDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>Creates the customer's account row on its own, so a first purchase never races to insert it.</summary>
    public async Task EnsureAccountAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (await _db.CreditAccounts.AnyAsync(a => a.CustomerId == customerId, cancellationToken))
            return;
        var account = new CreditAccount { CustomerId = customerId, Version = 1, UpdatedAt = _time.GetUtcNow() };
        _db.CreditAccounts.Add(account);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.Entry(account).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Loads the customer's account and bumps its version, so the caller's save fails if anyone else changed credits
    /// first. Call it before reading the batches you will change.
    /// </summary>
    public async Task<CreditAccount> LockAsync(Guid customerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var account = await _db.CreditAccounts.SingleOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);
        if (account is null)
        {
            account = new CreditAccount { CustomerId = customerId, Version = 1, UpdatedAt = now };
            _db.CreditAccounts.Add(account);
            return account;
        }
        account.Version++;
        account.UpdatedAt = now;
        return account;
    }

    /// <summary>Usable batches, earliest expiry first.</summary>
    public async Task<List<CreditBatch>> UsableAsync(Guid customerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var batches = await _db.CreditBatches
            .Where(b => b.CustomerId == customerId && b.Remaining > 0 && b.RefundedAt == null && b.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        return batches.OrderBy(b => b.ExpiresAt).ThenBy(b => b.PurchasedAt).ToList();
    }

    public async Task<CreditChoice> ChooseAsync(
        Guid customerId,
        decimal sessionRate,
        DateTimeOffset sessionStart,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        CreditRules.Choose(await UsableAsync(customerId, now, cancellationToken), sessionRate, sessionStart, now);

    public void AddPurchase(CreditBatch batch)
    {
        _db.CreditBatches.Add(batch);
        Add(batch, CreditEntryKind.Purchase, batch.Sessions, null, batch.PurchasedAt);
    }

    public void Use(CreditBatch batch, Guid bookingId, DateTimeOffset now)
    {
        if (!CreditRules.IsUsable(batch, now))
            throw new DomainException(CreditRules.LastCreditTakenMessage, 409);
        batch.Remaining--;
        Add(batch, CreditEntryKind.Use, -1, bookingId, now);
    }

    /// <summary>
    /// Gives a booking's credit back to the batch it came from, once. A batch that has expired or been refunded records the
    /// return with zero credits, so it never becomes usable again. Returns true if a usable credit came back.
    /// </summary>
    public async Task<bool> ReturnAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var batchId = booking.CreditBatchId ?? throw new InvalidOperationException("Booking was not paid with a credit.");
        if (await _db.CreditLedger.AnyAsync(e => e.BookingId == booking.Id && e.Kind == CreditEntryKind.Return, cancellationToken))
            throw new DomainException("This credit was already returned.", 409);

        await LockAsync(booking.CustomerId, now, cancellationToken);
        var batch = await _db.CreditBatches.SingleAsync(b => b.Id == batchId, cancellationToken);
        var usable = batch.RefundedAt is null && batch.ExpiresAt > now;
        if (usable)
            batch.Remaining++;
        Add(batch, CreditEntryKind.Return, usable ? 1 : 0, booking.Id, now);
        return usable;
    }

    public void Expire(CreditBatch batch, DateTimeOffset now)
    {
        var expired = batch.Remaining;
        batch.Remaining = 0;
        Add(batch, CreditEntryKind.Expire, -expired, null, now);
    }

    public void Refund(CreditBatch batch, decimal amount, DateTimeOffset now)
    {
        var unused = batch.Remaining;
        batch.Remaining = 0;
        batch.RefundedAmount = amount;
        batch.RefundedAt = now;
        Add(batch, CreditEntryKind.Refund, -unused, null, now);
    }

    private void Add(CreditBatch batch, CreditEntryKind kind, int credits, Guid? bookingId, DateTimeOffset now) =>
        _db.CreditLedger.Add(new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            CustomerId = batch.CustomerId,
            Kind = kind,
            Credits = credits,
            BookingId = bookingId,
            CreatedAt = now
        });
}

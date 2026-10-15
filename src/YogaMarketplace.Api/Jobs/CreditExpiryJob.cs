using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Expires unused credits daily at the admin-set market time (midnight by default). Writes one Expire row per batch
/// that is past its expiry and still has credits. Running twice is safe: an expired batch has none left, and the
/// database allows one Expire row per batch. Bookings already made with those credits stay.
/// </summary>
public class CreditExpiryJob : IBackgroundJobHandler
{
    public const string JobType = "credits.expire";

    public static readonly RecurringJob Recurring = new(JobType, terms => terms.CreditExpiry);

    private readonly YogaDbContext _db;
    private readonly CreditLedger _credits;
    private readonly ILogger<CreditExpiryJob> _logger;
    private readonly TimeProvider _time;

    public CreditExpiryJob(YogaDbContext db, CreditLedger credits, ILogger<CreditExpiryJob> logger, TimeProvider time)
    {
        _db = db;
        _credits = credits;
        _logger = logger;
        _time = time;
    }

    public string Type => JobType;

    public Task RunAsync(string? payload, CancellationToken cancellationToken) =>
        ExpireDueAsync(_time.GetUtcNow(), cancellationToken);

    public async Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await _db.CreditBatches.AsNoTracking()
            .Where(b => b.ExpiresAt <= now && b.Remaining > 0 && b.RefundedAt == null)
            .Select(b => new { b.Id, b.CustomerId })
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var item in due)
        {
            try
            {
                await _credits.LockAsync(item.CustomerId, now, cancellationToken);
                var batch = await _db.CreditBatches.SingleAsync(b => b.Id == item.Id, cancellationToken);
                if (batch.Remaining <= 0 || batch.RefundedAt is not null || batch.ExpiresAt > now)
                    continue;
                _credits.Expire(batch, now);
                await _db.SaveChangesAsync(cancellationToken);
                expired++;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogInformation(ex, "Credit batch {BatchId} changed while expiring; the next run picks it up.", item.Id);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }

        if (expired > 0)
            _logger.LogInformation("Expired unused credits on {Count} batches.", expired);
        return expired;
    }
}

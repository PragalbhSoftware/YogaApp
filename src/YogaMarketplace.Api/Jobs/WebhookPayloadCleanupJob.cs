using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Blanks the raw payload of webhook events older than the admin retention period, at midnight market time. The row
/// itself stays for the event log. Pending events keep their payload because they are replayed after the capture lands.
/// </summary>
public class WebhookPayloadCleanupJob : IBackgroundJobHandler
{
    public const string JobType = "webhooks.clear-payloads";
    private const int BatchSize = 500;

    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly BackgroundJobQueue _queue;
    private readonly ILogger<WebhookPayloadCleanupJob> _logger;

    public WebhookPayloadCleanupJob(
        YogaDbContext db,
        IPlatformSettingsService settings,
        BackgroundJobQueue queue,
        ILogger<WebhookPayloadCleanupJob> logger)
    {
        _db = db;
        _settings = settings;
        _queue = queue;
        _logger = logger;
    }

    public string Type => JobType;

    public async Task RunAsync(string? payload, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureScheduledAsync(now, cancellationToken);
        await ClearAsync(now, cancellationToken);
    }

    /// <summary>Queues the next run unless one is already waiting. Also called at startup.</summary>
    public async Task EnsureScheduledAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var next = CreditExpiryJob.NextRunAfter(now);
        var waiting = await _db.BackgroundJobs.AnyAsync(
            j => j.Type == JobType && j.Status == BackgroundJobStatus.Pending && j.RunAt >= next,
            cancellationToken);
        if (waiting)
            return;
        _queue.Enqueue(JobType, runAt: next);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-(await _settings.GetAsync(cancellationToken)).WebhookPayloadRetentionDays);
        // ponytail: dates are filtered in memory because SQLite cannot compare DateTimeOffset; only rows that still
        // hold a payload are read, so this stays near one retention period of events. Add a ReceivedAt index if it grows.
        var candidates = await _db.WebhookEvents.AsNoTracking()
            .Where(e => e.Payload != "" && e.Status != WebhookEventStatus.Pending)
            .Select(e => new { e.Id, e.ReceivedAt })
            .ToListAsync(cancellationToken);
        var due = candidates.Where(e => e.ReceivedAt < cutoff).Select(e => e.Id).ToList();

        var cleared = 0;
        foreach (var batch in due.Chunk(BatchSize))
        {
            cleared += await _db.WebhookEvents
                .Where(e => batch.Contains(e.Id) && e.Status != WebhookEventStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Payload, ""), cancellationToken);
        }

        if (cleared > 0)
            _logger.LogInformation("Cleared the payload of {Count} webhook events received before {Cutoff}.", cleared, cutoff);
        return cleared;
    }
}

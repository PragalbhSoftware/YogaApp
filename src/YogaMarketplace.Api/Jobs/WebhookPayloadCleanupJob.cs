using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Blanks the raw payload of processed webhook events older than the admin retention period, daily at the admin-set
/// market time (midnight by default). The row itself stays for the event log. Received, Pending and Failed events keep
/// their payload: they may still be handled, replayed or investigated.
/// </summary>
public class WebhookPayloadCleanupJob : IBackgroundJobHandler
{
    public const string JobType = "webhooks.clear-payloads";
    internal const int BatchSize = 500;

    public static readonly RecurringJob Recurring = new(JobType, terms => terms.WebhookCleanup);

    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly ILogger<WebhookPayloadCleanupJob> _logger;
    private readonly TimeProvider _time;

    public WebhookPayloadCleanupJob(
        YogaDbContext db,
        IPlatformSettingsService settings,
        ILogger<WebhookPayloadCleanupJob> logger,
        TimeProvider time)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
        _time = time;
    }

    public string Type => JobType;

    public Task RunAsync(string? payload, CancellationToken cancellationToken) =>
        ClearAsync(_time.GetUtcNow(), cancellationToken);

    public async Task<int> ClearAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-(await _settings.GetAsync(cancellationToken)).WebhookPayloadRetentionDays);
        // ponytail: dates are filtered in memory because SQLite cannot compare DateTimeOffset; only rows that still
        // hold a payload are read, so this stays near one retention period of events. Add a ReceivedAt index if it grows.
        var candidates = await _db.WebhookEvents.AsNoTracking()
            .Where(e => e.Payload != "" && e.Status == WebhookEventStatus.Processed)
            .Select(e => new { e.Id, e.ReceivedAt })
            .ToListAsync(cancellationToken);
        var due = candidates.Where(e => e.ReceivedAt < cutoff).Select(e => e.Id).ToList();

        var cleared = 0;
        foreach (var batch in due.Chunk(BatchSize))
        {
            cleared += await _db.WebhookEvents
                .Where(e => batch.Contains(e.Id) && e.Status == WebhookEventStatus.Processed)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Payload, ""), cancellationToken);
        }

        if (cleared > 0)
            _logger.LogInformation("Cleared the payload of {Count} webhook events received before {Cutoff}.", cleared, cutoff);
        return cleared;
    }
}

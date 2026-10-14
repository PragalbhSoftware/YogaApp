using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Sends one outbox row. Feature off, no SMTP, no address or no active template mark it Skipped. A placeholder with no
/// value marks it Failed at once, since retrying cannot fix the template. Any other error (SMTP, database, bad data) is
/// rethrown so the runner retries with its backoff; the row turns Failed on its last attempt, so it never stays Pending.
/// </summary>
public class NotificationSendJob : IBackgroundJobHandler
{
    public const string JobType = "notifications.send";

    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly IAlertSender _sender;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationSendJob> _logger;

    public NotificationSendJob(
        YogaDbContext db,
        IPlatformSettingsService settings,
        IAlertSender sender,
        TimeProvider time,
        ILogger<NotificationSendJob> logger)
    {
        _db = db;
        _settings = settings;
        _sender = sender;
        _time = time;
        _logger = logger;
    }

    public string Type => JobType;

    public async Task RunAsync(string? payload, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(payload, out var id))
            throw new InvalidOperationException("A notification job needs an outbox id.");
        var row = await _db.NotificationOutbox.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (row is null || row.Status != NotificationStatus.Pending)
            return;

        var attempt = ++row.Attempts;
        try
        {
            await ProcessAsync(row, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // SMTP errors often echo the address back, so only the masked text goes to the row, the logs and the runner.
            var error = Cut(LogMask.Text(ex.Message), NotificationOutbox.LastErrorMax);
            var status = attempt >= row.MaxAttempts ? NotificationStatus.Failed : NotificationStatus.Pending;
            await _db.NotificationOutbox
                .Where(o => o.Id == id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(o => o.Status, status)
                        .SetProperty(o => o.Attempts, attempt)
                        .SetProperty(o => o.LastError, error),
                    CancellationToken.None);
            _logger.LogWarning(
                "Notification {NotificationId} ({EventKey}) to {Recipient} failed on attempt {Attempt} of {MaxAttempts}: {Error}",
                row.Id, row.EventKey, LogMask.Text(row.Recipient ?? ""), attempt, row.MaxAttempts, error);
            throw new InvalidOperationException($"Notification failed: {error}");
        }
    }

    private async Task ProcessAsync(NotificationOutbox row, CancellationToken cancellationToken)
    {
        var enabled = (await _settings.GetAsync(cancellationToken)).Notifications.EmailEnabled;
        var template = await TemplateForAsync(row, cancellationToken);
        var skipReason = (enabled, _sender is NoopAlertSender, row.Recipient, template) switch
        {
            (false, _, _, _) => "Email notifications are off in admin settings.",
            (_, true, _, _) => "SMTP is not configured.",
            (_, _, null, _) => "No email on file.",
            (_, _, _, null) => "No active template.",
            _ => null
        };
        if (skipReason is not null)
        {
            await FinishAsync(row, NotificationStatus.Skipped, skipReason);
            _logger.LogInformation("Skipped notification {NotificationId} ({EventKey}): {Reason}", row.Id, row.EventKey, skipReason);
            return;
        }

        string subject, body;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Data) ?? [];
            subject = NotificationTemplateRules.Render(template!.Subject, data, singleLine: true);
            body = NotificationTemplateRules.Render(template.Body, data);
        }
        catch (KeyNotFoundException ex)
        {
            await FinishAsync(row, NotificationStatus.Failed, ex.Message);
            _logger.LogError("Notification {NotificationId} ({EventKey}) failed: {Reason}", row.Id, row.EventKey, ex.Message);
            return;
        }

        await _sender.SendAsync(row.Recipient!, subject, body, cancellationToken);
        row.SentAt = _time.GetUtcNow();
        await FinishAsync(row, NotificationStatus.Sent, null);
        _logger.LogInformation("Sent notification {NotificationId} ({EventKey}) to {Recipient}.", row.Id, row.EventKey, LogMask.Text(row.Recipient!));
    }

    private async Task<NotificationTemplate?> TemplateForAsync(NotificationOutbox row, CancellationToken cancellationToken)
    {
        var candidates = await _db.NotificationTemplates.AsNoTracking()
            .Where(t => t.EventKey == row.EventKey && t.Channel == row.Channel && t.IsActive)
            .ToListAsync(cancellationToken);
        return NotificationTemplateRules.Choose(candidates, row.CategoryId);
    }

    /// <summary>Saved without the job's token so a shutdown mid-send still records what happened.</summary>
    private Task FinishAsync(NotificationOutbox row, NotificationStatus status, string? error)
    {
        row.Status = status;
        row.LastError = error;
        return _db.SaveChangesAsync(CancellationToken.None);
    }

    private static string Cut(string value, int max) => value.Length > max ? value[..max] : value;
}

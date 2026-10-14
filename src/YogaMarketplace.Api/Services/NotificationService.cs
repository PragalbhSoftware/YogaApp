using System.Text.Json;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>Queues email only for now; SMS rows come with an SMS sender and DLT-registered templates.</summary>
public class NotificationService : INotificationService
{
    private readonly YogaDbContext _db;
    private readonly BackgroundJobQueue _jobs;
    private readonly IPlatformSettingsService _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        YogaDbContext db,
        BackgroundJobQueue jobs,
        IPlatformSettingsService settings,
        TimeProvider time,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _jobs = jobs;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        string eventKey,
        NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> data,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var maxAttempts = (await _settings.GetAsync(cancellationToken)).Notifications.MaxAttempts;
        var row = new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey,
            Channel = NotificationChannel.Email,
            CategoryId = categoryId,
            UserId = recipient.UserId,
            Recipient = string.IsNullOrWhiteSpace(recipient.Email) ? null : recipient.Email.Trim(),
            Data = JsonSerializer.Serialize(data),
            Status = NotificationStatus.Pending,
            MaxAttempts = maxAttempts,
            CreatedAt = _time.GetUtcNow()
        };
        _db.NotificationOutbox.Add(row);
        _jobs.Enqueue(NotificationSendJob.JobType, row.Id.ToString(), maxAttempts: maxAttempts);
        _logger.LogInformation(
            "Queued notification {NotificationId} ({EventKey}) for user {UserId} at {Recipient}.",
            row.Id,
            eventKey,
            recipient.UserId,
            row.Recipient is string email ? LogMask.Text(email) : LogMask.Phone(recipient.Phone ?? ""));
    }
}

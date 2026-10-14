using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Every few minutes (admin-set): deletes error rows not seen within the retention period, then, when email alerts
/// are on, emails the admin about each open error type seen since its last alert, at most once per throttle window.
/// A type still inside its window waits and goes out with the next run after the window ends.
/// </summary>
public class ErrorAlertJob : IBackgroundJobHandler
{
    public const string JobType = "errors.alert";
    private const int SubjectTypeMax = 120;

    public static readonly RecurringJob Recurring = new(JobType, terms => terms.ErrorAlerts);

    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly IAlertSender _sender;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ErrorAlertJob> _logger;
    private readonly TimeProvider _time;

    public ErrorAlertJob(
        YogaDbContext db,
        IPlatformSettingsService settings,
        IAlertSender sender,
        IHostEnvironment environment,
        ILogger<ErrorAlertJob> logger,
        TimeProvider time)
    {
        _db = db;
        _settings = settings;
        _sender = sender;
        _environment = environment;
        _logger = logger;
        _time = time;
    }

    public string Type => JobType;

    public Task RunAsync(string? payload, CancellationToken cancellationToken) =>
        RunAsync(_time.GetUtcNow(), cancellationToken);

    /// <summary>Returns how many alerts were sent.</summary>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var terms = (await _settings.GetAsync(cancellationToken)).ErrorAlerts;
        var cutoff = now.AddDays(-terms.RetentionDays);
        var deleted = await _db.ErrorEvents.Where(e => e.LastSeenAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} error types last seen before {Cutoff}.", deleted, cutoff);

        if (!terms.EmailEnabled || terms.Email is not string to)
            return 0;

        var quietSince = now.AddMinutes(-terms.ThrottleMinutes);
        var due = await _db.ErrorEvents
            .Where(e => e.AcknowledgedAt == null
                && (e.LastAlertedAt == null || (e.LastAlertedAt <= quietSince && e.LastSeenAt > e.LastAlertedAt)))
            .OrderBy(e => e.LastSeenAt)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var error in due)
        {
            try
            {
                await _sender.SendAsync(to, Subject(error), Body(error), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not send the error alert for {ErrorType}; the next run tries again.", error.ErrorType);
                continue;
            }
            error.LastAlertedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            sent++;
        }
        return sent;
    }

    private string Subject(ErrorEvent error)
    {
        var type = error.ErrorType.Length > SubjectTypeMax ? error.ErrorType[..SubjectTypeMax] + "…" : error.ErrorType;
        return $"[Yoga Marketplace {_environment.EnvironmentName}] {error.Level}: {type}";
    }

    private static string Body(ErrorEvent error) => string.Join(
        Environment.NewLine,
        $"Error type: {error.ErrorType}",
        $"Level: {error.Level}",
        $"Source: {error.Source}",
        $"Count since first seen: {error.Count}",
        $"First seen (UTC): {error.FirstSeenAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)}",
        $"Last seen (UTC): {error.LastSeenAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)}",
        $"Last correlation id: {error.LastCorrelationId}",
        "",
        error.LastMessage,
        "",
        "Open Admin › Errors to acknowledge it.");
}

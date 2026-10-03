using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

public class JobsOptions
{
    public const string Section = "Jobs";

    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 10;
    public int LockMinutes { get; set; } = 5;
}

/// <summary>Runs one job type. Register with <c>AddScoped&lt;IBackgroundJobHandler, MyHandler&gt;()</c>.</summary>
public interface IBackgroundJobHandler
{
    string Type { get; }

    Task RunAsync(string? payload, CancellationToken cancellationToken);
}

public class BackgroundJobQueue
{
    private readonly YogaDbContext _db;

    public BackgroundJobQueue(YogaDbContext db)
    {
        _db = db;
    }

    /// <summary>Adds the job to the context. It is saved with the caller's next <c>SaveChanges</c>, so it commits with the caller's work.</summary>
    public BackgroundJob Enqueue(string type, string? payload = null, DateTimeOffset? runAt = null, int maxAttempts = 5)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Job type is required.", nameof(type));
        var now = DateTimeOffset.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            RunAt = runAt ?? now,
            Status = BackgroundJobStatus.Pending,
            MaxAttempts = Math.Max(1, maxAttempts),
            CreatedAt = now
        };
        _db.BackgroundJobs.Add(job);
        return job;
    }
}

/// <summary>
/// Claims due jobs with a conditional UPDATE so two API instances never run the same job, then runs each
/// in its own scope. A failed job retries with exponential backoff until <see cref="BackgroundJob.MaxAttempts"/>.
/// A job whose lock expired (its worker died) goes back to Pending.
/// </summary>
public class BackgroundJobRunner
{
    private const int ErrorMax = 1000;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly YogaDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly JobsOptions _options;
    private readonly ILogger<BackgroundJobRunner> _logger;

    public BackgroundJobRunner(
        YogaDbContext db,
        IServiceScopeFactory scopes,
        IOptions<JobsOptions> options,
        ILogger<BackgroundJobRunner> logger)
    {
        _db = db;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    public string WorkerId { get; } = Truncate($"{Environment.MachineName}:{Guid.NewGuid():N}", 80);

    public async Task<int> RunDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await ReleaseExpiredLocksAsync(now, cancellationToken);

        var processed = 0;
        while (processed < _options.BatchSize && !cancellationToken.IsCancellationRequested)
        {
            var job = await ClaimNextAsync(now, cancellationToken);
            if (job is null)
                break;
            await ExecuteAsync(job, now, cancellationToken);
            processed++;
        }
        return processed;
    }

    public static TimeSpan BackoffFor(int attempts)
    {
        var minutes = Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromMinutes(Math.Min(minutes, MaxBackoff.TotalMinutes));
    }

    private Task<int> ReleaseExpiredLocksAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.BackgroundJobs
            .Where(j => j.Status == BackgroundJobStatus.Running && j.LockedUntil < now)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(j => j.Status, BackgroundJobStatus.Pending)
                    .SetProperty(j => j.LockedBy, (string?)null)
                    .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null),
                cancellationToken);

    private async Task<BackgroundJob?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await _db.BackgroundJobs.AsNoTracking()
            .Where(j => j.Status == BackgroundJobStatus.Pending && j.RunAt <= now)
            .OrderBy(j => j.RunAt)
            .Select(j => j.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        var lockedUntil = now.AddMinutes(_options.LockMinutes);
        foreach (var id in candidates)
        {
            var claimed = await _db.BackgroundJobs
                .Where(j => j.Id == id && j.Status == BackgroundJobStatus.Pending)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(j => j.Status, BackgroundJobStatus.Running)
                        .SetProperty(j => j.LockedBy, WorkerId)
                        .SetProperty(j => j.LockedUntil, lockedUntil)
                        .SetProperty(j => j.Attempts, j => j.Attempts + 1),
                    cancellationToken);
            if (claimed == 1)
                return await _db.BackgroundJobs.AsNoTracking().SingleAsync(j => j.Id == id, cancellationToken);
        }
        return null;
    }

    private async Task ExecuteAsync(BackgroundJob job, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>()
                .FirstOrDefault(h => string.Equals(h.Type, job.Type, StringComparison.Ordinal));
            if (handler is null)
            {
                await FinishAsync(job, BackgroundJobStatus.Failed, $"No handler is registered for job type '{job.Type}'.", null, cancellationToken);
                _logger.LogError("Background job {JobId} has unknown type {JobType}.", job.Id, job.Type);
                return;
            }

            await handler.RunAsync(job.Payload, cancellationToken);
            await FinishAsync(job, BackgroundJobStatus.Succeeded, null, null, cancellationToken);
            _logger.LogInformation("Background job {JobId} ({JobType}) succeeded on attempt {Attempt}.", job.Id, job.Type, job.Attempts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var error = Truncate(ex.Message, ErrorMax);
            if (job.Attempts >= job.MaxAttempts)
            {
                await FinishAsync(job, BackgroundJobStatus.Failed, error, null, cancellationToken);
                _logger.LogError(ex, "Background job {JobId} ({JobType}) failed for good after {Attempts} attempts.", job.Id, job.Type, job.Attempts);
            }
            else
            {
                var retryAt = now + BackoffFor(job.Attempts);
                await FinishAsync(job, BackgroundJobStatus.Pending, error, retryAt, cancellationToken);
                _logger.LogWarning(ex, "Background job {JobId} ({JobType}) failed on attempt {Attempt}; retrying at {RetryAt}.", job.Id, job.Type, job.Attempts, retryAt);
            }
        }
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    /// <summary>Only the worker that still holds the lock may record the outcome.</summary>
    private Task<int> FinishAsync(
        BackgroundJob job,
        BackgroundJobStatus status,
        string? error,
        DateTimeOffset? retryAt,
        CancellationToken cancellationToken)
    {
        var completedAt = status is BackgroundJobStatus.Succeeded or BackgroundJobStatus.Failed
            ? DateTimeOffset.UtcNow
            : (DateTimeOffset?)null;
        var runAt = retryAt ?? job.RunAt;
        return _db.BackgroundJobs
            .Where(j => j.Id == job.Id && j.LockedBy == WorkerId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(j => j.Status, status)
                    .SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.RunAt, runAt)
                    .SetProperty(j => j.CompletedAt, completedAt)
                    .SetProperty(j => j.LockedBy, (string?)null)
                    .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null),
                CancellationToken.None);
    }
}

public class BackgroundJobHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly JobsOptions _options;
    private readonly ILogger<BackgroundJobHostedService> _logger;

    public BackgroundJobHostedService(
        IServiceScopeFactory scopes,
        IOptions<JobsOptions> options,
        ILogger<BackgroundJobHostedService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunDueAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Background job poll failed.");
            }
        }
    }
}

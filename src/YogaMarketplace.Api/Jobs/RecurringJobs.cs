using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// A job the scheduler keeps queued, on the schedule the admin set. <see cref="PayloadFor"/> gets the run time.
/// Register with <c>AddSingleton(MyJob.Recurring)</c> next to the job's handler.
/// </summary>
public sealed record RecurringJob(
    string Type,
    Func<RecurringJobTerms, JobSchedule> Schedule,
    Func<DateTimeOffset, string?>? PayloadFor = null);

/// <summary>
/// Keeps exactly one queued run of each <see cref="RecurringJob"/>. It queues the next run when none is waiting, moves a
/// waiting run that has not started yet when its schedule changed, and drops it when the job is turned off. A run that
/// is already due, running or retrying is left alone. Called at startup, on every poll and after each run; the unique
/// <see cref="BackgroundJob.RecurringType"/> index stops two instances from queueing the same job twice.
/// </summary>
public class RecurringJobScheduler
{
    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly BackgroundJobQueue _queue;
    private readonly IEnumerable<RecurringJob> _jobs;
    private readonly ILogger<RecurringJobScheduler> _logger;

    public RecurringJobScheduler(
        YogaDbContext db,
        IPlatformSettingsService settings,
        BackgroundJobQueue queue,
        IEnumerable<RecurringJob> jobs,
        ILogger<RecurringJobScheduler> logger)
    {
        _db = db;
        _settings = settings;
        _queue = queue;
        _jobs = jobs;
        _logger = logger;
    }

    /// <summary>Checks every recurring job, or only <paramref name="type"/> when given.</summary>
    public async Task EnsureAsync(DateTimeOffset now, CancellationToken cancellationToken, string? type = null)
    {
        var terms = (await _settings.GetAsync(cancellationToken)).Jobs;
        foreach (var job in _jobs.Where(j => type is null || j.Type == type))
            await EnsureAsync(job, job.Schedule(terms), now, cancellationToken);
    }

    private async Task EnsureAsync(RecurringJob job, JobSchedule schedule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var queued = await _db.BackgroundJobs.AsNoTracking()
            .Where(j => j.RecurringType == job.Type)
            .Select(j => new { j.Id, j.Status, j.RunAt, j.Attempts })
            .SingleOrDefaultAsync(cancellationToken);
        if (queued is null)
        {
            if (!schedule.IsOff)
                await QueueAsync(job, schedule.NextRunAfter(now), cancellationToken);
            return;
        }

        var notStarted = _db.BackgroundJobs.Where(
            j => j.Id == queued.Id && j.Status == BackgroundJobStatus.Pending && j.Attempts == 0);
        if (schedule.IsOff)
        {
            if (await notStarted.ExecuteDeleteAsync(cancellationToken) == 1)
                _logger.LogInformation("Recurring job {JobType} is off; dropped its queued run.", job.Type);
            return;
        }
        if (queued.Status != BackgroundJobStatus.Pending || queued.Attempts > 0 || queued.RunAt <= now || schedule.Fits(queued.RunAt, now))
            return;

        var next = schedule.NextRunAfter(now);
        var payload = job.PayloadFor?.Invoke(next);
        if (await notStarted.ExecuteUpdateAsync(s => s.SetProperty(j => j.RunAt, next).SetProperty(j => j.Payload, payload), cancellationToken) == 1)
            _logger.LogInformation("Recurring job {JobType} moved from {OldRunAt} to {RunAt} after a schedule change.", job.Type, queued.RunAt, next);
    }

    private async Task QueueAsync(RecurringJob job, DateTimeOffset runAt, CancellationToken cancellationToken)
    {
        _queue.Enqueue(job.Type, job.PayloadFor?.Invoke(runAt), runAt).RecurringType = job.Type;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            if (!await _db.BackgroundJobs.AnyAsync(j => j.RecurringType == job.Type, cancellationToken))
                throw;
            _logger.LogDebug(ex, "Recurring job {JobType} was queued by another instance first.", job.Type);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>Lists background jobs for admin and lets an admin run a failed one again.</summary>
public class AdminJobService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ILogger<AdminJobService> _logger;
    private readonly TimeProvider _time;

    public AdminJobService(YogaDbContext db, ICurrentUser current, ILogger<AdminJobService> logger, TimeProvider time)
    {
        _db = db;
        _current = current;
        _logger = logger;
        _time = time;
    }

    public async Task<PagedResponse<AdminJobResponse>> ListAsync(string? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (pageNumber, size) = AdminQuery.Paging(page, pageSize);
        var parsed = AdminQuery.ParseOptional<BackgroundJobStatus>(status, "Status must be Pending, Running, Succeeded or Failed.");
        var query = _db.BackgroundJobs.AsNoTracking();
        if (parsed is not null)
            query = query.Where(j => j.Status == parsed);

        var total = await query.CountAsync(cancellationToken);
        var jobs = await query
            .OrderByDescending(j => j.RunAt)
            .ThenBy(j => j.Id)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new PagedResponse<AdminJobResponse>(jobs.Select(ToResponse).ToList(), total, pageNumber, size);
    }

    /// <summary>Queues a Failed job to run at once with a fresh set of attempts. It keeps its last error until it succeeds.</summary>
    public async Task<AdminJobResponse> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var retried = await _db.BackgroundJobs
            .Where(j => j.Id == id && j.Status == BackgroundJobStatus.Failed)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(j => j.Status, BackgroundJobStatus.Pending)
                    .SetProperty(j => j.Attempts, 0)
                    .SetProperty(j => j.RunAt, now)
                    .SetProperty(j => j.CompletedAt, (DateTimeOffset?)null),
                cancellationToken);
        var job = await _db.BackgroundJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new DomainException("Job not found.", 404);
        if (retried == 0)
            throw new DomainException("Only a failed job can be retried.", 409);

        _logger.LogInformation("Admin {AdminId} retried background job {JobId} ({JobType}).", _current.UserId, job.Id, job.Type);
        return ToResponse(job);
    }

    private static AdminJobResponse ToResponse(BackgroundJob job) => new(
        job.Id,
        job.Type,
        job.Payload,
        job.Status.ToString(),
        job.RunAt,
        job.Attempts,
        job.MaxAttempts,
        job.LastError,
        job.CreatedAt,
        job.CompletedAt,
        job.RecurringType is not null);
}

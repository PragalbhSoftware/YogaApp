using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class BackgroundJobRunnerTests : IClassFixture<YogaApiFactory>
{
    private readonly YogaApiFactory _factory;

    public BackgroundJobRunnerTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Backoff_doubles_each_attempt_and_stops_at_one_hour()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), BackgroundJobRunner.BackoffFor(1));
        Assert.Equal(TimeSpan.FromMinutes(2), BackgroundJobRunner.BackoffFor(2));
        Assert.Equal(TimeSpan.FromMinutes(16), BackgroundJobRunner.BackoffFor(5));
        Assert.Equal(TimeSpan.FromHours(1), BackgroundJobRunner.BackoffFor(20));
    }

    [Fact]
    public async Task Due_job_runs_once_and_a_future_job_waits_for_its_time()
    {
        await using var host = WithTestHandler();
        var now = WholeSecondNow();
        var due = await EnqueueAsync(host, TestJobHandler.JobType, "ok:" + Guid.NewGuid());
        var later = await EnqueueAsync(host, TestJobHandler.JobType, "ok:" + Guid.NewGuid(), runAt: now.AddHours(1));

        Assert.Equal(1, await RunAsync(host, now));
        var done = await JobAsync(host, due.Id);
        Assert.Equal(BackgroundJobStatus.Succeeded, done.Status);
        Assert.Equal(1, done.Attempts);
        Assert.NotNull(done.CompletedAt);
        Assert.Null(done.LockedBy);
        Assert.Equal(1, TestJobHandler.CallsFor(due.Payload!));
        Assert.Equal(BackgroundJobStatus.Pending, (await JobAsync(host, later.Id)).Status);

        Assert.Equal(0, await RunAsync(host, now));
        Assert.Equal(1, TestJobHandler.CallsFor(due.Payload!));

        Assert.Equal(1, await RunAsync(host, now.AddHours(2)));
        Assert.Equal(BackgroundJobStatus.Succeeded, (await JobAsync(host, later.Id)).Status);
    }

    [Fact]
    public async Task Failing_job_retries_with_backoff_then_fails_for_good()
    {
        await using var host = WithTestHandler();
        var now = WholeSecondNow();
        var job = await EnqueueAsync(host, TestJobHandler.JobType, "fail:" + Guid.NewGuid(), maxAttempts: 3);

        await RunAsync(host, now);
        var first = await JobAsync(host, job.Id);
        Assert.Equal(BackgroundJobStatus.Pending, first.Status);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(now.AddMinutes(1), first.RunAt);
        Assert.Contains("Test failure", first.LastError);
        Assert.Null(first.LockedBy);

        Assert.Equal(0, await RunAsync(host, now.AddSeconds(30)));

        await RunAsync(host, now.AddMinutes(1));
        var second = await JobAsync(host, job.Id);
        Assert.Equal(2, second.Attempts);
        Assert.Equal(now.AddMinutes(3), second.RunAt);

        await RunAsync(host, now.AddMinutes(3));
        var last = await JobAsync(host, job.Id);
        Assert.Equal(BackgroundJobStatus.Failed, last.Status);
        Assert.Equal(3, last.Attempts);
        Assert.NotNull(last.CompletedAt);
        Assert.Equal(3, TestJobHandler.CallsFor(job.Payload!));

        Assert.Equal(0, await RunAsync(host, now.AddDays(1)));
    }

    [Fact]
    public async Task Unknown_job_type_fails_without_retrying()
    {
        await using var host = WithTestHandler();
        var job = await EnqueueAsync(host, "test.missing", null);

        await RunAsync(host, WholeSecondNow());
        var failed = await JobAsync(host, job.Id);
        Assert.Equal(BackgroundJobStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Contains("No handler", failed.LastError);
    }

    [Fact]
    public async Task Job_whose_worker_died_is_picked_up_again_after_its_lock_expires()
    {
        await using var host = WithTestHandler();
        var now = WholeSecondNow();
        var payload = "ok:" + Guid.NewGuid();
        var id = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            db.BackgroundJobs.Add(new BackgroundJob
            {
                Id = id,
                Type = TestJobHandler.JobType,
                Payload = payload,
                RunAt = now.AddMinutes(-10),
                Status = BackgroundJobStatus.Running,
                Attempts = 1,
                LockedBy = "dead-worker",
                LockedUntil = now.AddMinutes(1),
                CreatedAt = now.AddMinutes(-10)
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await RunAsync(host, now));
        Assert.Equal(BackgroundJobStatus.Running, (await JobAsync(host, id)).Status);

        Assert.Equal(1, await RunAsync(host, now.AddMinutes(2)));
        var done = await JobAsync(host, id);
        Assert.Equal(BackgroundJobStatus.Succeeded, done.Status);
        Assert.Equal(2, done.Attempts);
        Assert.Equal(1, TestJobHandler.CallsFor(payload));
    }

    [Fact]
    public async Task Two_workers_never_run_the_same_job()
    {
        await using var host = WithTestHandler();
        var now = WholeSecondNow();
        var jobs = new List<BackgroundJob>();
        for (var i = 0; i < 6; i++)
            jobs.Add(await EnqueueAsync(host, TestJobHandler.JobType, "ok:" + Guid.NewGuid()));

        var processed = await Task.WhenAll(RunAsync(host, now), RunAsync(host, now));

        Assert.Equal(jobs.Count, processed.Sum());
        Assert.All(jobs, job => Assert.Equal(1, TestJobHandler.CallsFor(job.Payload!)));
    }

    /// <summary>Starts the host up front so its startup time never pushes an enqueue past the test's clock.</summary>
    private WebApplicationFactory<Program> WithTestHandler()
    {
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<IBackgroundJobHandler, TestJobHandler>()));
        _ = host.Services;
        return host;
    }

    /// <summary>The next whole second, so jobs enqueued just before it are already due.</summary>
    private static DateTimeOffset WholeSecondNow() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1);

    private static async Task<BackgroundJob> EnqueueAsync(
        WebApplicationFactory<Program> host,
        string type,
        string? payload,
        DateTimeOffset? runAt = null,
        int maxAttempts = 5)
    {
        using var scope = host.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<BackgroundJobQueue>().Enqueue(type, payload, runAt, maxAttempts);
        await scope.ServiceProvider.GetRequiredService<YogaDbContext>().SaveChangesAsync();
        return job;
    }

    private static async Task<int> RunAsync(WebApplicationFactory<Program> host, DateTimeOffset now)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunDueAsync(now, CancellationToken.None);
    }

    private static async Task<BackgroundJob> JobAsync(WebApplicationFactory<Program> host, Guid id)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<YogaDbContext>().BackgroundJobs.AsNoTracking().SingleAsync(j => j.Id == id);
    }

    /// <summary>Succeeds for payloads starting with "ok:" and throws for anything else.</summary>
    private sealed class TestJobHandler : IBackgroundJobHandler
    {
        public const string JobType = "test.echo";
        private static readonly ConcurrentDictionary<string, int> Calls = new();

        public string Type => JobType;

        public static int CallsFor(string payload) => Calls.GetValueOrDefault(payload);

        public Task RunAsync(string? payload, CancellationToken cancellationToken)
        {
            Calls.AddOrUpdate(payload ?? "", 1, (_, count) => count + 1);
            if (payload?.StartsWith("ok:", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Test failure");
            return Task.CompletedTask;
        }
    }
}

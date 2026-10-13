using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>Each test gets its own database: the queued run of a recurring job is shared by the whole app.</summary>
public class RecurringJobsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] RecurringTypes = [CreditExpiryJob.JobType, ReconciliationJob.JobType, WebhookPayloadCleanupJob.JobType];

    [Fact]
    public async Task Each_recurring_job_has_one_queued_run_at_its_default_time_even_with_two_instances_and_a_restart()
    {
        await using var factory = new YogaApiFactory();
        var now = DateTimeOffset.UtcNow;
        await using var second = factory.WithWebHostBuilder(_ => { });

        await Task.WhenAll(EnsureAsync(factory, now), EnsureAsync(second, now), EnsureAsync(factory, now), EnsureAsync(second, now));
        await using (var restarted = factory.WithWebHostBuilder(_ => { }))
            await EnsureAsync(restarted, now.AddMinutes(1));

        var queued = await QueryAsync(factory, db => db.BackgroundJobs.AsNoTracking().ToListAsync());
        foreach (var type in RecurringTypes)
        {
            var run = Assert.Single(queued, j => j.Type == type);
            Assert.Equal((BackgroundJobStatus.Pending, type), (run.Status, run.RecurringType));
        }
        var nextMidnight = MumbaiClock.SessionStart(MumbaiClock.Today(now).AddDays(1), TimeOnly.MinValue);
        Assert.Equal(nextMidnight, queued.Single(j => j.Type == CreditExpiryJob.JobType).RunAt);
        Assert.Equal(nextMidnight, queued.Single(j => j.Type == WebhookPayloadCleanupJob.JobType).RunAt);
        Assert.Equal(JobSchedule.Daily(new TimeOnly(6, 0)).NextRunAfter(now), queued.Single(j => j.Type == ReconciliationJob.JobType).RunAt);

        var duplicate = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Type = CreditExpiryJob.JobType,
            RunAt = now,
            CreatedAt = now,
            RecurringType = CreditExpiryJob.JobType
        };
        await Assert.ThrowsAsync<DbUpdateException>(() => QueryAsync(factory, db =>
        {
            db.BackgroundJobs.Add(duplicate);
            return db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task A_job_set_to_run_daily_at_two_am_market_time_runs_once_a_day_even_after_a_restart()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
        await PatchSettingsAsync(admin, new { creditExpiryTime = "02:00" });
        var now = DateTimeOffset.UtcNow;

        await EnsureAsync(factory, now);
        var first = await QueuedAsync(factory, CreditExpiryJob.JobType);
        Assert.Equal(JobSchedule.Daily(new TimeOnly(2, 0)).NextRunAfter(now), first.RunAt);
        Assert.Equal(new TimeOnly(2, 0), MarketTime(first.RunAt));

        await using (var restarted = factory.WithWebHostBuilder(_ => { }))
        {
            await EnsureAsync(restarted, now.AddMinutes(5));
            Assert.Equal(first.Id, (await QueuedAsync(restarted, CreditExpiryJob.JobType)).Id);
            await RunDueAsync(restarted, first.RunAt);
        }

        var next = await QueuedAsync(factory, CreditExpiryJob.JobType);
        Assert.Equal(first.RunAt.AddDays(1), next.RunAt);
        Assert.Equal(new TimeOnly(2, 0), MarketTime(next.RunAt));
        var done = await JobAsync(factory, first.Id);
        Assert.Equal((BackgroundJobStatus.Succeeded, (string?)null), (done.Status, done.RecurringType));

        await using (var restarted = factory.WithWebHostBuilder(_ => { }))
        {
            await EnsureAsync(restarted, first.RunAt.AddMinutes(1));
            await RunDueAsync(restarted, first.RunAt.AddHours(1));
        }
        var runs = await QueryAsync(factory, db => db.BackgroundJobs.AsNoTracking().Where(j => j.Type == CreditExpiryJob.JobType).ToListAsync());
        Assert.Equal(2, runs.Count);
        Assert.Equal(next.Id, Assert.Single(runs, j => j.Status == BackgroundJobStatus.Pending).Id);
    }

    [Fact]
    public async Task A_changed_time_moves_the_queued_run_and_turning_a_job_off_drops_it()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
        var now = DateTimeOffset.UtcNow;
        await EnsureAsync(factory, now);
        var queued = await QueuedAsync(factory, WebhookPayloadCleanupJob.JobType);

        await PatchSettingsAsync(admin, new { webhookCleanupTime = "03:15" });
        await EnsureAsync(factory, now);
        var moved = await QueuedAsync(factory, WebhookPayloadCleanupJob.JobType);
        Assert.Equal(queued.Id, moved.Id);
        Assert.Equal(JobSchedule.Daily(new TimeOnly(3, 15)).NextRunAfter(now), moved.RunAt);

        await PatchSettingsAsync(admin, new { webhookCleanupEnabled = false });
        await EnsureAsync(factory, now);
        Assert.False(await QueryAsync(factory, db => db.BackgroundJobs.AnyAsync(j => j.Type == WebhookPayloadCleanupJob.JobType)));
        Assert.Single(await QueryAsync(factory, db => db.BackgroundJobs.Where(j => j.Type == CreditExpiryJob.JobType).ToListAsync()));

        await PatchSettingsAsync(admin, new { webhookCleanupEnabled = true });
        await EnsureAsync(factory, now);
        Assert.Equal(moved.RunAt, (await QueuedAsync(factory, WebhookPayloadCleanupJob.JobType)).RunAt);
    }

    [Fact]
    public async Task A_recurring_job_that_fails_for_good_queues_its_next_run_shows_in_admin_and_can_be_retried()
    {
        await using var factory = new YogaApiFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<IBackgroundJobHandler, FlakyHandler>();
            services.AddSingleton(new RecurringJob(FlakyHandler.JobType, _ => JobSchedule.Every(60)));
        }));
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        await EnsureAsync(host, DateTimeOffset.UtcNow);
        var failing = await QueuedAsync(host, FlakyHandler.JobType);

        FlakyHandler.Fail = true;
        try
        {
            for (var attempt = 0; attempt < failing.MaxAttempts; attempt++)
                await RunDueAsync(host, (await JobAsync(host, failing.Id)).RunAt);
        }
        finally
        {
            FlakyHandler.Fail = false;
        }

        var failed = await JobAsync(host, failing.Id);
        Assert.Equal((BackgroundJobStatus.Failed, failing.MaxAttempts, (string?)null), (failed.Status, failed.Attempts, failed.RecurringType));
        var next = await QueuedAsync(host, FlakyHandler.JobType);
        Assert.NotEqual(failing.Id, next.Id);
        Assert.Equal((BackgroundJobStatus.Pending, 0), (next.Status, next.Attempts));

        var listed = await admin.GetFromJsonAsync<PageBody>("/api/admin/jobs?status=Failed", Json);
        var row = Assert.Single(listed!.Items, j => j.Id == failing.Id);
        Assert.Equal((FlakyHandler.JobType, "Failed", FlakyHandler.Error), (row.Type, row.Status, row.LastError));
        Assert.DoesNotContain(listed.Items, j => j.Status != "Failed");

        var retry = await admin.PostAsync($"/api/admin/jobs/{failing.Id}/retry", null);
        Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        var retried = await JobAsync(host, failing.Id);
        Assert.Equal((BackgroundJobStatus.Pending, 0, (DateTimeOffset?)null), (retried.Status, retried.Attempts, retried.CompletedAt));
        Assert.Equal(FlakyHandler.Error, retried.LastError);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/jobs/{failing.Id}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/admin/jobs/{Guid.NewGuid()}/retry", null)).StatusCode);

        await RunDueAsync(host, retried.RunAt.AddSeconds(1));
        var succeeded = await JobAsync(host, failing.Id);
        Assert.Equal((BackgroundJobStatus.Succeeded, 1, (string?)null), (succeeded.Status, succeeded.Attempts, succeeded.LastError));
        Assert.Equal(next.Id, (await QueuedAsync(host, FlakyHandler.JobType)).Id);

        var customer = await SignInAsync(host, SeedIds.AnanyaPhone);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/jobs")).StatusCode);
    }

    [Fact]
    public void Every_n_minutes_runs_n_minutes_later_and_a_waiting_run_moves_only_when_the_interval_shrank()
    {
        var now = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var every = JobSchedule.Every(15);

        Assert.Equal(now.AddMinutes(15), every.NextRunAfter(now));
        Assert.True(every.Fits(now.AddMinutes(10), now));
        Assert.False(every.Fits(now.AddMinutes(30), now));
        Assert.Throws<ArgumentOutOfRangeException>(() => JobSchedule.Every(0));
        Assert.Throws<InvalidOperationException>(() => JobSchedule.Off.NextRunAfter(now));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(new PlatformSettings(), new PlatformSettingsChanges(CreditExpiryTime: new TimeOnly(2, 0, 30))));
    }

    private static TimeOnly MarketTime(DateTimeOffset at) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, MumbaiClock.Zone).DateTime);

    private static async Task EnsureAsync(WebApplicationFactory<Program> host, DateTimeOffset now)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RecurringJobScheduler>().EnsureAsync(now, CancellationToken.None);
    }

    private static async Task RunDueAsync(WebApplicationFactory<Program> host, DateTimeOffset now)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunDueAsync(now, CancellationToken.None);
    }

    private static Task<BackgroundJob> QueuedAsync(WebApplicationFactory<Program> host, string type) =>
        QueryAsync(host, db => db.BackgroundJobs.AsNoTracking().SingleAsync(j => j.RecurringType == type));

    private static Task<BackgroundJob> JobAsync(WebApplicationFactory<Program> host, Guid id) =>
        QueryAsync(host, db => db.BackgroundJobs.AsNoTracking().SingleAsync(j => j.Id == id));

    private static async Task<T> QueryAsync<T>(WebApplicationFactory<Program> host, Func<YogaDbContext, Task<T>> query)
    {
        using var scope = host.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static async Task PatchSettingsAsync(HttpClient admin, object changes)
    {
        var current = await admin.GetFromJsonAsync<SettingsBody>("/api/admin/settings", Json);
        var body = JsonSerializer.SerializeToNode(changes, Json)!.AsObject();
        body["version"] = current!.Version;
        var saved = await admin.PatchAsJsonAsync("/api/admin/settings", body, Json);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string phone)
    {
        var client = host.CreateClient();
        var otp = await (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = false }))
            .Content.ReadFromJsonAsync<OtpBody>(Json);
        var auth = await (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = otp!.DevCode }))
            .Content.ReadFromJsonAsync<AuthBody>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record SettingsBody(int Version);
    private sealed record JobBody(Guid Id, string Type, string Status, int Attempts, string? LastError);
    private sealed record PageBody(List<JobBody> Items, int Total);

    private sealed class FlakyHandler : IBackgroundJobHandler
    {
        public const string JobType = "test.flaky";
        public const string Error = "Flaky handler failed.";
        public static volatile bool Fail;

        public string Type => JobType;

        public Task RunAsync(string? payload, CancellationToken cancellationToken) =>
            Fail ? throw new InvalidOperationException(Error) : Task.CompletedTask;
    }
}

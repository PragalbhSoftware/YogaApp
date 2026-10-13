using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;
using YogaMarketplace.Infrastructure.Persistence.Migrations;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Each test gets its own database, because the stuck-webhook and pending-refund checks look at every row, not one day.
/// The Razorpay side is a stub that returns whatever the test lists.
/// </summary>
public class ReconciliationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly Day = new(2026, 1, 10);
    private static readonly DateTimeOffset InWindow = ReconciliationRules.WindowFor(Day).Start.AddHours(10);
    private static readonly DateTimeOffset BeforeWindow = InWindow.AddDays(-5);

    [Fact]
    public async Task Clean_day_gives_zero_items_and_no_alert()
    {
        await using var host = new Host();
        var booking = await host.SeedPaymentAsync("pay_clean", 500m, PaymentStatus.PartiallyRefunded, InWindow, refunded: 100m, refundId: "rfnd_clean");
        await host.SeedPackAsync("pay_clean_pack", 2000m, InWindow);
        host.Gateway.Payments.Add(Captured("pay_clean", 500m, refunded: 100m));
        host.Gateway.Payments.Add(Captured("pay_clean_pack", 2000m));
        host.Gateway.Payments.Add(new RazorpayPaymentRecord("pay_abandoned", "order_x", "failed", 50000, 0, "INR"));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_clean", "pay_clean", 10000, "processed", booking.ToString("N")));

        var run = await host.ReconcileAsync(Day);

        Assert.Equal(ReconciliationRunStatus.Completed, run.Status);
        Assert.True(run.GatewayCompared);
        Assert.Equal(0, run.ItemCount);
        Assert.Empty(await host.ItemsAsync());
        Assert.Empty(host.Logs.Errors);
    }

    [Fact]
    public async Task Payment_in_razorpay_with_no_booking_is_reported_and_raises_one_alert()
    {
        await using var host = new Host();
        host.Gateway.Payments.Add(Captured("pay_orphan", 799m));
        host.Gateway.Payments.Add(Captured("pay_orphan_2", 299m));

        var run = await host.ReconcileAsync(Day);

        var items = await host.ItemsAsync();
        Assert.Equal(2, run.ItemCount);
        Assert.All(items, item => Assert.Equal(ReconciliationItemType.MissingInDatabase, item.Type));
        Assert.Contains(items, item => item.GatewayPaymentId == "pay_orphan" && item.GatewayValue == "799.00 captured");
        var alert = Assert.Single(host.Logs.Errors);
        Assert.Contains(run.Id.ToString(), alert, StringComparison.Ordinal);
        Assert.Contains("2 items", alert, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Amount_status_and_refund_mismatches_are_reported_by_payment_id()
    {
        await using var host = new Host();
        await host.SeedPaymentAsync("pay_amount", 500m, PaymentStatus.Paid, InWindow);
        await host.SeedPaymentAsync("pay_status", 500m, PaymentStatus.Paid, InWindow);
        await host.SeedPaymentAsync("pay_refund", 500m, PaymentStatus.Paid, InWindow);
        host.Gateway.Payments.Add(Captured("pay_amount", 450m));
        host.Gateway.Payments.Add(new RazorpayPaymentRecord("pay_status", "order_pay_status", "failed", 50000, 0, "INR"));
        host.Gateway.Payments.Add(Captured("pay_refund", 500m, refunded: 200m));

        await host.ReconcileAsync(Day);

        var items = (await host.ItemsAsync()).ToDictionary(i => i.GatewayPaymentId!);
        Assert.Equal(3, items.Count);
        Assert.Equal(ReconciliationItemType.AmountMismatch, items["pay_amount"].Type);
        Assert.Equal(("500.00", "450.00"), (items["pay_amount"].DatabaseValue, items["pay_amount"].GatewayValue));
        Assert.Equal(ReconciliationItemType.StatusMismatch, items["pay_status"].Type);
        Assert.Equal(ReconciliationItemType.RefundMismatch, items["pay_refund"].Type);
        Assert.Equal(("0.00", "200.00"), (items["pay_refund"].DatabaseValue, items["pay_refund"].GatewayValue));
        Assert.Single(host.Logs.Errors);
    }

    [Fact]
    public async Task Database_payment_razorpay_does_not_have_is_missing_in_razorpay_after_a_direct_lookup()
    {
        await using var host = new Host();
        await host.SeedPaymentAsync("pay_ghost", 500m, PaymentStatus.Paid, InWindow);
        await host.SeedPaymentAsync("pay_late_listed", 500m, PaymentStatus.Paid, InWindow);
        host.Gateway.Fetchable.Add(Captured("pay_late_listed", 500m));

        await host.ReconcileAsync(Day);

        var item = Assert.Single(await host.ItemsAsync());
        Assert.Equal(ReconciliationItemType.MissingInRazorpay, item.Type);
        Assert.Equal("pay_ghost", item.GatewayPaymentId);
    }

    [Fact]
    public async Task Unrecorded_refunds_are_split_into_ours_and_dashboard_ones()
    {
        await using var host = new Host();
        await host.SeedPaymentAsync("pay_dashboard", 500m, PaymentStatus.Paid, BeforeWindow);
        var lostCommit = await host.SeedPaymentAsync("pay_lost_commit", 500m, PaymentStatus.Paid, BeforeWindow);
        var packBatch = await host.SeedPackAsync("pay_pack_lost", 2000m, BeforeWindow);
        await host.SeedPaymentAsync("pay_known", 500m, PaymentStatus.Refunded, BeforeWindow, refunded: 500m, refundId: "rfnd_known");
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_dash", "pay_dashboard", 20000, "processed", null));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_ours", "pay_lost_commit", 50000, "processed", lostCommit.ToString("N")));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_pack", "pay_pack_lost", 100000, "processed", packBatch.ToString("N")));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_known", "pay_known", 50000, "processed", null));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_failed", "pay_dashboard", 100, "failed", null));

        await host.ReconcileAsync(Day);

        var items = (await host.ItemsAsync()).ToDictionary(i => i.GatewayRefundId!);
        Assert.Equal(3, items.Count);
        Assert.Equal(ReconciliationItemType.DashboardRefund, items["rfnd_dash"].Type);
        Assert.Equal(ReconciliationItemType.RefundNotRecorded, items["rfnd_ours"].Type);
        Assert.Equal(ReconciliationItemType.RefundNotRecorded, items["rfnd_pack"].Type);
    }

    [Fact]
    public async Task Stuck_webhooks_and_pending_refunds_use_the_admin_thresholds_and_are_reported_once()
    {
        await using var host = new Host();
        await host.SetThresholdsAsync(webhookMinutes: 30, refundHours: 24);
        var now = DateTimeOffset.UtcNow;
        await host.SeedWebhookAsync("evt_stuck", now.AddMinutes(-45), WebhookEventStatus.Received);
        await host.SeedWebhookAsync("evt_fresh", now.AddMinutes(-10), WebhookEventStatus.Received);
        await host.SeedWebhookAsync("evt_done", now.AddHours(-5), WebhookEventStatus.Processed);
        var stale = await host.SeedPaymentAsync("pay_pending_old", 500m, PaymentStatus.Refunded, now.AddHours(-30),
            refunded: 500m, refundId: "rfnd_slow", refundStatus: RefundStatus.Pending);
        var fresh = await host.SeedPaymentAsync("pay_pending_new", 500m, PaymentStatus.Refunded, now.AddHours(-2),
            refunded: 500m, refundId: "rfnd_new", refundStatus: RefundStatus.Pending);

        await host.ReconcileAsync(Day, now);

        var items = await host.ItemsAsync();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Type == ReconciliationItemType.StuckWebhook && i.Reference == "evt_stuck");
        Assert.Contains(items, i => i.Type == ReconciliationItemType.RefundPending && i.Reference == stale.ToString());

        var again = await host.ReconcileAsync(Day.AddDays(1), now.AddDays(1));
        Assert.Equal(
            new[] { "evt_fresh", fresh.ToString() }.Order(),
            (await host.ItemsAsync(again.Id)).Select(i => i.Reference!).Order());
    }

    [Fact]
    public async Task Razorpay_failure_marks_the_run_failed_keeps_no_items_and_lets_the_job_retry()
    {
        await using var host = new Host();
        await host.SeedWebhookAsync("evt_stuck_fail", DateTimeOffset.UtcNow.AddHours(-3), WebhookEventStatus.Received);
        host.Gateway.Failure = new DomainException("Could not read payments from Razorpay. Try again.", 502);

        await Assert.ThrowsAsync<DomainException>(() => host.ReconcileAsync(Day));

        var run = Assert.Single(await host.RunsAsync());
        Assert.Equal(ReconciliationRunStatus.Failed, run.Status);
        Assert.Contains("Razorpay", run.Error, StringComparison.Ordinal);
        Assert.Empty(await host.ItemsAsync());
        Assert.Empty(host.Logs.Errors);
    }

    [Fact]
    public async Task Fake_gateway_skips_the_razorpay_comparison_and_still_runs_database_checks()
    {
        await using var host = new Host(useStub: false);
        await host.SeedPaymentAsync("pay_fake_only", 500m, PaymentStatus.Paid, InWindow);
        await host.SeedWebhookAsync("evt_fake_stuck", DateTimeOffset.UtcNow.AddHours(-3), WebhookEventStatus.Received);

        var run = await host.ReconcileAsync(Day);

        Assert.False(run.GatewayCompared);
        Assert.Equal(ReconciliationItemType.StuckWebhook, Assert.Single(await host.ItemsAsync()).Type);
        Assert.Single(host.Logs.Infos, line => line.Contains("skipped the Razorpay comparison", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Daily_job_is_queued_at_the_admin_time_for_the_previous_market_day_and_runs_on_schedule()
    {
        await using var host = new Host();
        await host.SetRunTimeAsync(new TimeOnly(7, 30));
        var now = DateTimeOffset.UtcNow;

        await host.EnsureScheduledAsync(now);
        await host.EnsureScheduledAsync(now);

        var job = Assert.Single(await host.JobsAsync(BackgroundJobStatus.Pending));
        var expected = ReconciliationRules.NextRunAfter(now, new TimeOnly(7, 30));
        Assert.Equal(expected, job.RunAt);
        Assert.Equal(TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(job.RunAt, MumbaiClock.Zone).DateTime), new TimeOnly(7, 30));
        var marketDay = MumbaiClock.Today(job.RunAt).AddDays(-1);
        Assert.Equal(marketDay.ToString("yyyy-MM-dd"), job.Payload);
        host.Gateway.Payments.Add(Captured("pay_scheduled_orphan", 100m));

        await host.RunJobsAsync(job.RunAt);

        var run = Assert.Single(await host.RunsAsync());
        Assert.Equal(marketDay, run.MarketDate);
        Assert.Equal(1, run.ItemCount);
        Assert.Single(host.Logs.Errors);
        var next = Assert.Single(await host.JobsAsync(BackgroundJobStatus.Pending));
        Assert.True(next.RunAt > job.RunAt);
    }

    [Fact]
    public void Next_run_is_today_at_the_set_time_if_still_ahead_otherwise_tomorrow()
    {
        var day = new DateOnly(2026, 3, 1);
        var at = new TimeOnly(6, 0);
        var before = MumbaiClock.SessionStart(day, new TimeOnly(5, 59));
        var after = MumbaiClock.SessionStart(day, new TimeOnly(6, 0));

        Assert.Equal(MumbaiClock.SessionStart(day, at), ReconciliationRules.NextRunAfter(before, at));
        Assert.Equal(MumbaiClock.SessionStart(day.AddDays(1), at), ReconciliationRules.NextRunAfter(after, at));
    }

    [Fact]
    public void Settings_validate_the_reconciliation_time_and_thresholds()
    {
        var settings = new PlatformSettings();
        Assert.Equal(new TimeOnly(6, 0), settings.ReconciliationTime);
        Assert.Equal(60, settings.WebhookStuckMinutes);
        Assert.Equal(48, settings.RefundPendingHours);

        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(WebhookStuckMinutes: 0)));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(RefundPendingHours: PlatformSettingsRules.MaxRefundPendingHours + 1)));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(ReconciliationTime: new TimeOnly(6, 0, 30))));

        var changes = PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(
            ReconciliationTime: new TimeOnly(4, 15), WebhookStuckMinutes: 90, RefundPendingHours: 72));
        Assert.Equal(
            new[] { "ReconciliationTime 06:00 -> 04:15", "WebhookStuckMinutes 60 -> 90", "RefundPendingHours 48 -> 72" },
            changes.Select(c => $"{c.Field} {c.OldValue} -> {c.NewValue}").ToArray());
    }

    [Fact]
    public void Resolve_needs_a_note_and_only_happens_once()
    {
        var item = new ReconciliationItem();
        var admin = Guid.NewGuid();
        Assert.Throws<DomainException>(() => ReconciliationRules.Resolve(item, admin, "  ", DateTimeOffset.UtcNow));

        ReconciliationRules.Resolve(item, admin, " Refunded by ops in dashboard ", DateTimeOffset.UtcNow);
        Assert.Equal(ReconciliationItemStatus.Resolved, item.Status);
        Assert.Equal("Refunded by ops in dashboard", item.ResolutionNote);
        Assert.Equal(409, Assert.Throws<DomainException>(() => ReconciliationRules.Resolve(item, admin, "again", DateTimeOffset.UtcNow)).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_runs_filters_items_and_marks_one_resolved_with_a_note()
    {
        await using var host = new Host();
        await host.SeedPaymentAsync("pay_admin_amount", 500m, PaymentStatus.Paid, InWindow);
        host.Gateway.Payments.Add(Captured("pay_admin_amount", 400m));
        host.Gateway.Payments.Add(Captured("pay_admin_orphan", 100m));
        var run = await host.ReconcileAsync(Day);
        var admin = await host.AdminClientAsync();

        var runs = await admin.GetFromJsonAsync<PageBody<RunBody>>("/api/admin/reconciliation/runs", Json);
        var listed = Assert.Single(runs!.Items);
        Assert.Equal((run.Id, 2, 2, "Completed"), (listed.Id, listed.ItemCount, listed.OpenCount, listed.Status));

        var orphans = await admin.GetFromJsonAsync<PageBody<ItemBody>>($"/api/admin/reconciliation/items?runId={run.Id}&type=MissingInDatabase", Json);
        var orphan = Assert.Single(orphans!.Items);
        Assert.Equal("pay_admin_orphan", orphan.GatewayPaymentId);

        var blank = await admin.PostAsJsonAsync($"/api/admin/reconciliation/items/{orphan.Id}/resolve", new { note = "" });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var resolved = await admin.PostAsJsonAsync($"/api/admin/reconciliation/items/{orphan.Id}/resolve", new { note = "Test payment, refunded in dashboard." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var body = (await resolved.Content.ReadFromJsonAsync<ItemBody>(Json))!;
        Assert.Equal(("Resolved", "Test payment, refunded in dashboard."), (body.Status, body.ResolutionNote));
        Assert.False(string.IsNullOrWhiteSpace(body.ResolvedByName));

        var open = await admin.GetFromJsonAsync<PageBody<ItemBody>>("/api/admin/reconciliation/items?status=Open", Json);
        Assert.Equal("AmountMismatch", Assert.Single(open!.Items).Type);
        var badType = await admin.GetAsync("/api/admin/reconciliation/items?type=Nope");
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        var customer = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/admin/reconciliation/runs")).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_are_paged_newest_first_and_the_page_size_is_capped()
    {
        await using var host = new Host();
        host.Gateway.Payments.Add(Captured("pay_paged_orphan", 100m));
        var now = DateTimeOffset.UtcNow;
        var runs = new List<ReconciliationRun>();
        for (var i = 0; i < 3; i++)
            runs.Add(await host.ReconcileAsync(Day.AddDays(i), now.AddMinutes(i)));
        var admin = await host.AdminClientAsync();

        var newestRun = await admin.GetFromJsonAsync<PageBody<RunBody>>("/api/admin/reconciliation/runs?page=1&pageSize=1", Json);
        Assert.Equal((3, 1, 1), (newestRun!.Total, newestRun.Page, newestRun.PageSize));
        Assert.Equal(runs[2].Id, Assert.Single(newestRun.Items).Id);
        var oldestRun = await admin.GetFromJsonAsync<PageBody<RunBody>>("/api/admin/reconciliation/runs?page=3&pageSize=1", Json);
        Assert.Equal(runs[0].Id, Assert.Single(oldestRun!.Items).Id);

        var first = await admin.GetFromJsonAsync<PageBody<ItemBody>>("/api/admin/reconciliation/items?pageSize=2", Json);
        var second = await admin.GetFromJsonAsync<PageBody<ItemBody>>("/api/admin/reconciliation/items?pageSize=2&page=2", Json);
        Assert.Equal((3, 2, 1), (first!.Total, first.Items.Count, second!.Items.Count));
        Assert.Equal(new[] { runs[2].Id, runs[1].Id, runs[0].Id }, first.Items.Concat(second.Items).Select(i => i.RunId));

        var capped = await admin.GetFromJsonAsync<PageBody<ItemBody>>("/api/admin/reconciliation/items?pageSize=100000&page=0", Json);
        Assert.Equal((1, AdminQuery.PageSize, 3), (capped!.Page, capped.PageSize, capped.Items.Count));
    }

    [Fact]
    public async Task A_second_run_for_a_completed_day_returns_the_saved_one_without_a_second_report()
    {
        await using var host = new Host();
        host.Gateway.Payments.Add(Captured("pay_twice_orphan", 100m));

        var first = await host.ReconcileAsync(Day);
        var second = await host.ReconcileAsync(Day);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Id, Assert.Single(await host.RunsAsync()).Id);
        Assert.Single(await host.ItemsAsync());
        Assert.Single(host.Logs.Errors);
        Assert.Single(host.Logs.Infos, line => line.Contains("already has completed run", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_runs_started_at_once_save_one_completed_run_and_one_report()
    {
        await using var host = new Host();
        host.Gateway.Payments.Add(Captured("pay_race_orphan", 100m));

        var arrived = 0;
        var bothListing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Gateway.BeforeList = () =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
                bothListing.TrySetResult();
            return bothListing.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        var both = await Task.WhenAll(host.ReconcileAsync(Day), host.ReconcileAsync(Day));

        var saved = Assert.Single(await host.RunsAsync());
        Assert.All(both, run => Assert.Equal(saved.Id, run.Id));
        Assert.Equal(saved.Id, Assert.Single(await host.ItemsAsync()).RunId);
        Assert.Single(host.Logs.Errors);
        Assert.Single(host.Logs.Infos, line => line.Contains("already has completed run", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unique_index_migration_keeps_the_latest_completed_run_per_day_and_down_restores_them()
    {
        await using var factory = new YogaApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        await db.Database.ExecuteSqlRawAsync($"DROP INDEX {ReconciliationRules.OneCompletedRunPerDayIndex};");
        var (start, end) = ReconciliationRules.WindowFor(Day);
        ReconciliationRun Run(DateOnly day, DateTimeOffset startedAt, ReconciliationRunStatus status) => new()
        {
            Id = Guid.NewGuid(), MarketDate = day, WindowStart = start, WindowEnd = end,
            StartedAt = startedAt, CompletedAt = startedAt, Status = status
        };
        var older = Run(Day, InWindow, ReconciliationRunStatus.Completed);
        var latest = Run(Day, InWindow.AddHours(1), ReconciliationRunStatus.Completed);
        var failed = Run(Day, InWindow.AddHours(2), ReconciliationRunStatus.Failed);
        var otherDay = Run(Day.AddDays(1), InWindow, ReconciliationRunStatus.Completed);
        db.ReconciliationRuns.AddRange(older, latest, failed, otherDay);
        await db.SaveChangesAsync();

        var migration = new AddReconciliationRunUniqueAndPackRefundId { ActiveProvider = db.Database.ProviderName };
        foreach (var operation in migration.UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(operation.Sql);
        Assert.Equal(
            [ReconciliationRunStatus.Superseded, ReconciliationRunStatus.Completed, ReconciliationRunStatus.Failed, ReconciliationRunStatus.Completed],
            await StatusesAsync());

        foreach (var operation in migration.DownOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(operation.Sql);
        Assert.Equal(
            [ReconciliationRunStatus.Completed, ReconciliationRunStatus.Completed, ReconciliationRunStatus.Failed, ReconciliationRunStatus.Completed],
            await StatusesAsync());

        async Task<ReconciliationRunStatus[]> StatusesAsync()
        {
            var statuses = await db.ReconciliationRuns.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Status);
            return [statuses[older.Id], statuses[latest.Id], statuses[failed.Id], statuses[otherDay.Id]];
        }
    }

    [Fact]
    public async Task Pack_refund_made_in_the_app_is_matched_by_id_and_a_same_amount_dashboard_refund_is_flagged()
    {
        await using var host = new Host();
        await host.SeedPackAsync("pay_pack_twice", 2000m, BeforeWindow, refunded: 500m, refundId: "rfnd_pack_app");
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_pack_app", "pay_pack_twice", 50000, "processed", null));
        host.Gateway.Refunds.Add(new RazorpayRefundRecord("rfnd_pack_dash", "pay_pack_twice", 50000, "processed", null));

        await host.ReconcileAsync(Day);

        var item = Assert.Single(await host.ItemsAsync());
        Assert.Equal((ReconciliationItemType.DashboardRefund, "rfnd_pack_dash"), (item.Type, item.GatewayRefundId));
    }

    [Fact]
    public async Task Payment_captured_just_before_midnight_and_saved_just_after_is_clean_on_both_days()
    {
        await using var host = new Host();
        var nextDay = Day.AddDays(1);
        var savedAt = MumbaiClock.SessionStart(nextDay, new TimeOnly(0, 0, 5));
        await host.SeedPaymentAsync("pay_midnight", 500m, PaymentStatus.Paid, savedAt);
        // Razorpay lists it on the day it was captured (23:59:59); the next day only a direct fetch finds it.
        host.Gateway.Payments.Add(Captured("pay_midnight", 500m));

        var capturedDay = await host.ReconcileAsync(Day);
        host.Gateway.Payments.Clear();
        host.Gateway.Fetchable.Add(Captured("pay_midnight", 500m));
        var savedDay = await host.ReconcileAsync(nextDay);

        Assert.Equal((0, 0), (capturedDay.ItemCount, savedDay.ItemCount));
        Assert.Empty(host.Logs.Errors);
    }

    [Fact]
    public async Task A_failed_run_is_followed_by_a_completed_retry_for_the_same_day()
    {
        await using var host = new Host();
        host.Gateway.Payments.Add(Captured("pay_retry_orphan", 100m));
        host.Gateway.Failure = new DomainException("Could not read payments from Razorpay. Try again.", 502);
        await Assert.ThrowsAsync<DomainException>(() => host.ReconcileAsync(Day));

        host.Gateway.Failure = null;
        var retry = await host.ReconcileAsync(Day);

        var runs = await host.RunsAsync();
        Assert.Equal(
            new[] { ReconciliationRunStatus.Completed, ReconciliationRunStatus.Failed }.Order(),
            runs.Select(r => r.Status).Order());
        Assert.Equal(ReconciliationRunStatus.Completed, retry.Status);
        Assert.Equal(retry.Id, Assert.Single(await host.ItemsAsync()).RunId);
    }

    [Fact]
    public async Task Razorpay_rate_limit_halfway_through_paging_fails_the_run_with_no_partial_report()
    {
        var handler = new RazorpayListPagingTests.PagedHandler(total: 250, throttleAtSkip: 100);
        await using var host = new Host(gateway: RazorpayListPagingTests.NewClient(handler));

        await Assert.ThrowsAsync<DomainException>(() => host.ReconcileAsync(Day));

        Assert.Equal(new[] { 0, 100 }, handler.Skips);
        Assert.Equal(ReconciliationRunStatus.Failed, Assert.Single(await host.RunsAsync()).Status);
        Assert.Empty(await host.ItemsAsync());
    }

    [Fact]
    public async Task Admin_settings_round_trip_the_reconciliation_fields()
    {
        await using var host = new Host();
        var admin = await host.AdminClientAsync();

        var saved = await AdminSettingsClient.PatchAsync(admin, version => new
        {
            version,
            reconciliationTime = "05:45",
            webhookStuckMinutes = 15,
            refundPendingHours = 12
        });
        var current = await admin.GetFromJsonAsync<ReconciliationSettingsBody>("/api/admin/settings", Json);
        Assert.Equal(("05:45", 15, 12), (current!.ReconciliationTime, current.WebhookStuckMinutes, current.RefundPendingHours));
        Assert.True(saved.Version > 1);

        var bad = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = saved.Version, reconciliationTime = "25:00" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    private static RazorpayPaymentRecord Captured(string id, decimal amount, decimal refunded = 0m) =>
        new(id, "order_" + id, refunded >= amount ? "refunded" : "captured", (long)(amount * 100), (long)(refunded * 100), "INR");

    private sealed record PageBody<T>(List<T> Items, int Total, int Page, int PageSize);
    private sealed record RunBody(Guid Id, string Status, int ItemCount, int OpenCount);
    private sealed record ItemBody(Guid Id, Guid RunId, string Type, string Status, string? GatewayPaymentId, string? ResolutionNote, string? ResolvedByName);
    private sealed record ReconciliationSettingsBody(string ReconciliationTime, int WebhookStuckMinutes, int RefundPendingHours);
    private sealed record OtpBody(string? DevCode);
    private sealed record AuthBody(string Token);

    /// <summary>A fresh API with its own database, the stub gateway (unless told otherwise) and captured logs.</summary>
    private sealed class Host : IAsyncDisposable
    {
        private readonly YogaApiFactory _factory = new();
        private readonly WebApplicationFactory<Program> _host;
        private int _slots;

        public Host(bool useStub = true, IRazorpayClient? gateway = null)
        {
            _host = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.AddProvider(Logs));
                if (useStub)
                    builder.ConfigureServices(services => services.AddSingleton<IRazorpayClient>(gateway ?? Gateway));
            });
        }

        public StubGateway Gateway { get; } = new();
        public LogCapture Logs { get; } = new();

        public HttpClient CreateClient() => _host.CreateClient();

        public async Task<ReconciliationRun> ReconcileAsync(DateOnly day, DateTimeOffset? now = null)
        {
            using var scope = _host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ReconciliationJob>()
                .ReconcileAsync(day, now ?? DateTimeOffset.UtcNow, CancellationToken.None);
        }

        public async Task EnsureScheduledAsync(DateTimeOffset now)
        {
            using var scope = _host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReconciliationJob>().EnsureScheduledAsync(now, CancellationToken.None);
        }

        public async Task RunJobsAsync(DateTimeOffset now)
        {
            using var scope = _host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunDueAsync(now, CancellationToken.None);
        }

        public Task<List<BackgroundJob>> JobsAsync(BackgroundJobStatus status) =>
            QueryAsync(db => db.BackgroundJobs.AsNoTracking().Where(j => j.Type == ReconciliationJob.JobType && j.Status == status).ToListAsync());

        public Task<List<ReconciliationRun>> RunsAsync() => QueryAsync(db => db.ReconciliationRuns.AsNoTracking().ToListAsync());

        public Task<List<ReconciliationItem>> ItemsAsync(Guid? runId = null) =>
            QueryAsync(db => db.ReconciliationItems.AsNoTracking().Where(i => runId == null || i.RunId == runId).ToListAsync());

        public Task SetThresholdsAsync(int webhookMinutes, int refundHours) => WriteAsync(async db =>
        {
            var settings = await db.Settings.SingleAsync();
            settings.WebhookStuckMinutes = webhookMinutes;
            settings.RefundPendingHours = refundHours;
        });

        public Task SetRunTimeAsync(TimeOnly at) => WriteAsync(async db => (await db.Settings.SingleAsync()).ReconciliationTime = at);

        public Task SeedWebhookAsync(string eventId, DateTimeOffset receivedAt, WebhookEventStatus status) => WriteAsync(db =>
        {
            db.WebhookEvents.Add(new WebhookEvent
            {
                Id = Guid.NewGuid(),
                Provider = PaymentGateways.Razorpay,
                EventId = eventId,
                EventType = "payment.captured",
                Payload = "{}",
                ReceivedAt = receivedAt,
                Status = status
            });
            return Task.CompletedTask;
        });

        /// <summary>Adds a paid booking and its Razorpay payment. Returns the booking id, which our refunds use as receipt.</summary>
        public async Task<Guid> SeedPaymentAsync(
            string paymentId,
            decimal amount,
            PaymentStatus status,
            DateTimeOffset createdAt,
            decimal refunded = 0m,
            string? refundId = null,
            RefundStatus? refundStatus = null)
        {
            var bookingId = Guid.NewGuid();
            var index = Interlocked.Increment(ref _slots);
            await WriteAsync(db =>
            {
                var customer = NewCustomer(db);
                var slot = new AvailabilitySlot
                {
                    Id = Guid.NewGuid(),
                    ProviderId = SeedIds.AnanyaProviderId,
                    Mode = SessionMode.Online,
                    Date = MumbaiClock.Today().AddDays(200 + index),
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(7, 0)
                };
                db.AvailabilitySlots.Add(slot);
                db.Bookings.Add(new Booking
                {
                    Id = bookingId,
                    CustomerId = customer.Id,
                    ProviderId = slot.ProviderId,
                    ServiceId = SeedIds.AnanyaServiceId,
                    SlotId = slot.Id,
                    Mode = slot.Mode,
                    Status = BookingStatus.Upcoming,
                    Amount = amount,
                    MeetLinkSnapshot = "https://meet.google.com/abc-defg-hij",
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
                db.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    Amount = amount,
                    RefundedAmount = refunded,
                    Status = status,
                    Gateway = PaymentGateways.Razorpay,
                    GatewayOrderId = "order_" + paymentId,
                    GatewayPaymentId = paymentId,
                    GatewayRefundId = refundId,
                    RefundStatus = refundStatus,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
                return Task.CompletedTask;
            });
            return bookingId;
        }

        /// <summary>Adds a pack sale paid through Razorpay. Returns the batch id, which a pack refund uses as receipt.</summary>
        public async Task<Guid> SeedPackAsync(string paymentId, decimal price, DateTimeOffset purchasedAt, decimal refunded = 0m, string? refundId = null)
        {
            var batchId = Guid.NewGuid();
            await WriteAsync(db =>
            {
                var customer = NewCustomer(db);
                var pack = new CreditPack { Id = Guid.NewGuid(), Name = "Recon pack", Sessions = 5, Price = price, ValidityDays = 90, PriceCap = 999m, CreatedAt = purchasedAt };
                var checkout = new CheckoutIntent
                {
                    Id = Guid.NewGuid(),
                    Kind = CheckoutKind.Pack,
                    CustomerId = customer.Id,
                    PackId = pack.Id,
                    Amount = price,
                    GatewayOrderId = "order_" + paymentId,
                    GatewayPaymentId = paymentId,
                    Status = CheckoutStatus.Completed,
                    CreatedAt = purchasedAt
                };
                db.CreditPacks.Add(pack);
                db.CheckoutIntents.Add(checkout);
                db.CreditBatches.Add(new CreditBatch
                {
                    Id = batchId,
                    CustomerId = customer.Id,
                    PackId = pack.Id,
                    CheckoutIntentId = checkout.Id,
                    PackName = pack.Name,
                    Sessions = pack.Sessions,
                    Price = price,
                    ValidityDays = pack.ValidityDays,
                    PriceCap = pack.PriceCap,
                    GatewayPaymentId = paymentId,
                    RefundedAmount = refunded,
                    GatewayRefundId = refundId,
                    PurchasedAt = purchasedAt,
                    ExpiresAt = purchasedAt.AddDays(pack.ValidityDays),
                    Remaining = pack.Sessions
                });
                return Task.CompletedTask;
            });
            return batchId;
        }

        public async Task<HttpClient> AdminClientAsync()
        {
            var client = _host.CreateClient();
            var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone = SeedIds.AdminPhone, isNewUser = false });
            var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone = SeedIds.AdminPhone, code = otp.DevCode });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await _host.DisposeAsync();
            await _factory.DisposeAsync();
        }

        private static User NewCustomer(YogaDbContext db)
        {
            var customer = new User
            {
                Id = Guid.NewGuid(),
                Phone = "+9197" + Random.Shared.Next(10000000, 99999999),
                Name = "Reconciliation Customer",
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(customer);
            return customer;
        }

        private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
        {
            var response = await client.PostAsJsonAsync(path, body);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, payload);
            return JsonSerializer.Deserialize<T>(payload, Json)!;
        }

        private async Task<T> QueryAsync<T>(Func<YogaDbContext, Task<T>> query)
        {
            using var scope = _host.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
        }

        private async Task WriteAsync(Func<YogaDbContext, Task> write)
        {
            using var scope = _host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            await write(db);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Razorpay as the test describes it. <see cref="Failure"/> makes every list call throw.</summary>
    private sealed class StubGateway : IRazorpayClient
    {
        public List<RazorpayPaymentRecord> Payments { get; } = [];
        public List<RazorpayRefundRecord> Refunds { get; } = [];
        public List<RazorpayPaymentRecord> Fetchable { get; } = [];
        public Exception? Failure { get; set; }

        /// <summary>Awaited before listing payments, so a test can hold runs at the same point.</summary>
        public Func<Task>? BeforeList { get; set; }

        public string KeyId => "rzp_test_stub";
        public bool SupportsLocalCapture => false;

        public async Task<IReadOnlyList<RazorpayPaymentRecord>> ListPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        {
            if (BeforeList is not null)
                await BeforeList();
            return Failure is null ? Payments.ToList() : throw Failure;
        }

        public Task<IReadOnlyList<RazorpayRefundRecord>> ListRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult<IReadOnlyList<RazorpayRefundRecord>>(Refunds.ToList()) : Task.FromException<IReadOnlyList<RazorpayRefundRecord>>(Failure);

        public Task<RazorpayPaymentRecord?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken) =>
            Task.FromResult(Fetchable.Concat(Payments).FirstOrDefault(p => p.Id == paymentId));

        public Task<RazorpayCreatedOrder> CreateOrderAsync(long amountPaise, string currency, string receipt, IReadOnlyDictionary<string, string> notes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RequireCapturedCheckoutAsync(string orderId, string paymentId, string signature, long expectedAmountPaise, string expectedCurrency, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public (string PaymentId, string Signature) CreateLocalCapture(string orderId) => throw new NotSupportedException();

        public bool VerifyWebhookSignature(string rawBody, string? signature) => false;

        public Task<string?> RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>Keeps the reconciliation job's Error and Information lines.</summary>
    private sealed class LogCapture : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

        public IEnumerable<string> Errors => Lines(LogLevel.Error);
        public IEnumerable<string> Infos => Lines(LogLevel.Information);

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(ReconciliationJob).FullName ? new Logger(_lines) : NullLogger.Instance;

        public void Dispose()
        {
        }

        private IEnumerable<string> Lines(LogLevel level) => _lines.Where(l => l.Level == level).Select(l => l.Message).ToList();

        private sealed class Logger(ConcurrentQueue<(LogLevel, string)> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}

public class RazorpayListPagingTests
{
    [Fact]
    public async Task List_payments_reads_every_page_until_a_short_one()
    {
        var handler = new PagedHandler(total: 203);
        var client = NewClient(handler);
        var from = DateTimeOffset.FromUnixTimeSeconds(1_767_000_000);

        var payments = await client.ListPaymentsAsync(from, from.AddDays(1), CancellationToken.None);

        Assert.Equal(203, payments.Count);
        Assert.Equal(203, payments.Select(p => p.Id).Distinct().Count());
        Assert.Equal(new[] { 0, 100, 200 }, handler.Skips);
        Assert.All(handler.Paths, path => Assert.StartsWith("/v1/payments?from=1767000000&to=1767086400&count=100&skip=", path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task List_refunds_stops_after_one_page_when_it_is_short()
    {
        var handler = new PagedHandler(total: 3);
        var refunds = await NewClient(handler).ListRefundsAsync(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), CancellationToken.None);

        Assert.Equal(3, refunds.Count);
        Assert.Equal("pay_1", refunds[1].PaymentId);
        Assert.Equal(new[] { 0 }, handler.Skips);
    }

    [Fact]
    public async Task A_razorpay_error_or_timeout_becomes_a_domain_error()
    {
        var error = NewClient(new PagedHandler(total: 0, status: HttpStatusCode.InternalServerError));
        Assert.Equal(502, (await Assert.ThrowsAsync<DomainException>(() =>
            error.ListPaymentsAsync(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), CancellationToken.None))).StatusCode);

        var timeout = NewClient(new PagedHandler(total: 0, timeout: true));
        Assert.Equal(503, (await Assert.ThrowsAsync<DomainException>(() =>
            timeout.ListRefundsAsync(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), CancellationToken.None))).StatusCode);
    }

    internal const string KeySecret = "secret";

    internal static RazorpayHttpClient NewClient(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.razorpay.test/") },
        Microsoft.Extensions.Options.Options.Create(new RazorpayOptions { KeyId = "rzp_test_paging", KeySecret = KeySecret }),
        NullLogger<RazorpayHttpClient>.Instance);

    /// <summary>
    /// Serves <c>total</c> items in pages of the requested count, starting at the requested skip.
    /// <c>throttleAtSkip</c> answers that page with a Razorpay 429.
    /// </summary>
    internal sealed class PagedHandler(int total, HttpStatusCode status = HttpStatusCode.OK, bool timeout = false, int? throttleAtSkip = null)
        : HttpMessageHandler
    {
        public List<int> Skips { get; } = [];
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (timeout)
                throw new TaskCanceledException("Timed out.");
            var uri = request.RequestUri!;
            Paths.Add(uri.PathAndQuery);
            var query = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
            var skip = int.Parse(query["skip"]);
            var count = int.Parse(query["count"]);
            Skips.Add(skip);
            if (skip == throttleAtSkip)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") });
            var items = Enumerable.Range(skip, Math.Max(0, Math.Min(count, total - skip))).Select(i => new
            {
                id = $"item_{i}",
                order_id = $"order_{i}",
                payment_id = $"pay_{i}",
                status = "captured",
                amount = 10000,
                amount_refunded = 0,
                currency = "INR",
                receipt = (string?)null
            });
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(new { entity = "collection", count = items.Count(), items })
            });
        }
    }
}

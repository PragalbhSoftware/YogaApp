using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class ConsentTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ConsentTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_new_account_needs_consent_and_stores_it_with_the_policy_version()
    {
        var client = _factory.CreateClient();
        var phone = NewPhone();

        var refused = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, name = "Asha Rao", gender = "Female", isNewUser = true });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Terms", (await refused.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);
        var declined = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, name = "Asha Rao", gender = "Female", isNewUser = true, acceptedPolicies = false });
        Assert.Equal(HttpStatusCode.BadRequest, declined.StatusCode);
        Assert.Contains("Privacy Policy", (await declined.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

        await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Asha Rao", gender = "Female", isNewUser = true, acceptedPolicies = true });
        var resent = await PostAsync<OtpBody>(client, "/api/auth/otp/resend", new { phone });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = resent.DevCode });

        Assert.False(auth.User.ConsentRequired);
        var version = await PolicyVersionAsync();
        var records = await RecordsAsync(auth.User.Id);
        Assert.Equal(ConsentRules.Purposes, records.Select(r => r.Purpose).Order());
        Assert.All(records, r => Assert.Equal(version, r.PolicyVersion));
    }

    [Fact]
    public async Task A_code_issued_without_consent_cannot_create_an_account()
    {
        var phone = NewPhone();
        await WriteAsync(db => db.OtpChallenges.Add(new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            CodeHash = OtpHasher.Hash("123456", "test-otp-pepper"),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            IsNewUser = true,
            PendingName = "Old Code",
            PendingGender = Gender.Other,
            CreatedAt = DateTimeOffset.UtcNow
        }));

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await QueryAsync(db => db.Users.AnyAsync(u => u.Phone == phone)));
    }

    [Fact]
    public async Task A_policy_bump_asks_existing_users_once_and_stores_one_record_per_version()
    {
        var customer = await SignUpAsync();
        var admin = await SignInAsync(SeedIds.AdminPhone);
        Assert.True((await MeAsync(admin)).ConsentRequired);
        await PostAsync<UserBody>(admin, "/api/auth/consent", new { });

        var settings = await admin.GetFromJsonAsync<SettingsBody>("/api/admin/settings", Json);
        var bumped = settings!.PolicyVersion + 1;
        var lower = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = settings.Version, policyVersion = settings.PolicyVersion - 1 });
        Assert.Equal(HttpStatusCode.BadRequest, lower.StatusCode);
        var saved = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = settings.Version, policyVersion = bumped });
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());

        var me = await MeAsync(customer.Client);
        Assert.True(me.ConsentRequired);

        var accepted = await PostAsync<UserBody>(customer.Client, "/api/auth/consent", new { });
        Assert.False(accepted.ConsentRequired);
        await PostAsync<UserBody>(customer.Client, "/api/auth/consent", new { });

        var records = await RecordsAsync(me.Id);
        Assert.Equal(2, records.Count(r => r.PolicyVersion == bumped));
        Assert.Equal(4, records.Length);

        var again = await SignInAsync(customer.Phone);
        Assert.False((await MeAsync(again)).ConsentRequired);
    }

    [Fact]
    public void Consent_is_required_until_every_purpose_reaches_the_current_version()
    {
        ConsentRecord Record(ConsentPurpose purpose, int version) => new() { Purpose = purpose, PolicyVersion = version };

        Assert.True(ConsentRules.IsRequired([], 1));
        Assert.True(ConsentRules.IsRequired([Record(ConsentPurpose.Terms, 2)], 2));
        Assert.True(ConsentRules.IsRequired([Record(ConsentPurpose.Terms, 2), Record(ConsentPurpose.Privacy, 1)], 2));
        Assert.False(ConsentRules.IsRequired([Record(ConsentPurpose.Terms, 2), Record(ConsentPurpose.Privacy, 1), Record(ConsentPurpose.Privacy, 2)], 2));
    }

    [Fact]
    public void Retention_and_policy_version_are_validated_and_audited()
    {
        var settings = new PlatformSettings { PolicyVersion = 3 };

        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(WebhookPayloadRetentionDays: 0)));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings,
            new PlatformSettingsChanges(WebhookPayloadRetentionDays: PlatformSettingsRules.MaxWebhookPayloadRetentionDays + 1)));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(PolicyVersion: 2)));
        Assert.Equal(PlatformSettingsRules.DefaultWebhookPayloadRetentionDays, settings.WebhookPayloadRetentionDays);

        var log = PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(PolicyVersion: 4, WebhookPayloadRetentionDays: 30));

        Assert.Equal([nameof(PlatformSettings.PolicyVersion), nameof(PlatformSettings.WebhookPayloadRetentionDays)], log.Select(c => c.Field));
        Assert.Equal(4, settings.PolicyVersion);
        Assert.Equal(30, settings.WebhookPayloadRetentionDays);
    }

    [Fact]
    public async Task Cleanup_blanks_only_processed_payloads_strictly_older_than_the_retention_setting()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var settings = await admin.GetFromJsonAsync<SettingsBody>("/api/admin/settings", Json);
        const int days = 30;
        var saved = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = settings!.Version, webhookPayloadRetentionDays = days });
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-days);
        var old = cutoff.AddSeconds(-1);
        var oldProcessed = await SeedWebhooksAsync(WebhookPayloadCleanupJob.BatchSize + 1, old, WebhookEventStatus.Processed);
        var kept = new[]
        {
            (await SeedWebhooksAsync(1, old, WebhookEventStatus.Received))[0],
            (await SeedWebhooksAsync(1, old, WebhookEventStatus.Pending))[0],
            (await SeedWebhooksAsync(1, old, WebhookEventStatus.Failed))[0],
            (await SeedWebhooksAsync(1, cutoff, WebhookEventStatus.Processed))[0],
            (await SeedWebhooksAsync(1, now.AddDays(-1), WebhookEventStatus.Processed))[0]
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<WebhookPayloadCleanupJob>();
            Assert.Equal(oldProcessed.Length, await job.ClearAsync(now, CancellationToken.None));
            Assert.Equal(0, await job.ClearAsync(now, CancellationToken.None));
            await scope.ServiceProvider.GetRequiredService<RecurringJobScheduler>().EnsureAsync(now, CancellationToken.None);
        }

        var ids = oldProcessed.Concat(kept).ToList();
        var rows = await QueryAsync(db => db.WebhookEvents.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id));
        Assert.Equal(ids.Count, rows.Count);
        Assert.All(oldProcessed, id => Assert.Equal("", rows[id].Payload));
        Assert.All(kept, id => Assert.Equal("{}", rows[id].Payload));
        Assert.All(rows.Values, row => Assert.Equal("evt_" + row.Id.ToString("N"), row.EventId));
        Assert.All(oldProcessed, id => Assert.Equal(WebhookEventStatus.Processed, rows[id].Status));
        Assert.True(await QueryAsync(db => db.BackgroundJobs.AnyAsync(
            j => j.Type == WebhookPayloadCleanupJob.JobType && j.Status == BackgroundJobStatus.Pending)));
    }

    private async Task<Guid[]> SeedWebhooksAsync(int count, DateTimeOffset receivedAt, WebhookEventStatus status)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        await WriteAsync(db => db.WebhookEvents.AddRange(ids.Select(id => new WebhookEvent
        {
            Id = id,
            Provider = PaymentGateways.Razorpay,
            EventId = "evt_" + id.ToString("N"),
            EventType = "payment.captured",
            Payload = "{}",
            ReceivedAt = receivedAt,
            Status = status
        })));
        return ids;
    }

    private async Task<(HttpClient Client, string Phone)> SignUpAsync()
    {
        var client = _factory.CreateClient();
        var phone = NewPhone();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Consent User", gender = "Other", isNewUser = true, acceptedPolicies = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, phone);
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<UserBody> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<UserBody>("/api/auth/me", Json))!;

    private Task<int> PolicyVersionAsync() => QueryAsync(db => db.Settings.Select(s => s.PolicyVersion).SingleAsync());

    private Task<ConsentRecord[]> RecordsAsync(Guid userId) =>
        QueryAsync(db => db.ConsentRecords.AsNoTracking().Where(c => c.UserId == userId).ToArrayAsync());

    private async Task WriteAsync(Action<YogaDbContext> write)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        write(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> QueryAsync<T>(Func<YogaDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static string NewPhone() => "+9194" + Random.Shared.Next(10000000, 99999999);

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(string? DevCode);
    private sealed record UserBody(Guid Id, bool ConsentRequired);
    private sealed record AuthBody(string Token, UserBody User);
    private sealed record SettingsBody(int Version, int PolicyVersion);
}

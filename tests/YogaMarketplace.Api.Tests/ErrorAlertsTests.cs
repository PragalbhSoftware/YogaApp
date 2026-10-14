using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>Alert tests get their own database: the alert job reads every open error in it.</summary>
public class ErrorAlertsTests
{
    private const string AdminEmail = "ops@example.com";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Correlation_id_from_the_caller_comes_back_in_the_header_and_the_error_body()
    {
        await using var factory = new YogaApiFactory();
        var client = factory.CreateClient();

        var notFound = await SendAsync(client, HttpMethod.Get, "/api/no-such-endpoint", "support-ticket-42");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("support-ticket-42", Header(notFound));
        Assert.Equal("support-ticket-42", await BodyIdAsync(notFound));

        var domainError = await SendAsync(client, HttpMethod.Post, "/api/auth/otp/request", "abc.123_x", new { phone = "123", isNewUser = false });
        Assert.Equal(HttpStatusCode.BadRequest, domainError.StatusCode);
        Assert.Equal("abc.123_x", Header(domainError));
        Assert.Equal("abc.123_x", await BodyIdAsync(domainError));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has spaces")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task Missing_or_unsafe_correlation_id_is_replaced_with_a_new_one(string? sent)
    {
        await using var factory = new YogaApiFactory();

        var response = await SendAsync(factory.CreateClient(), HttpMethod.Get, "/api/no-such-endpoint", sent);

        var id = Header(response);
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Equal(id, await BodyIdAsync(response));
    }

    [Theory]
    [InlineData("+919876543210", "*********3210")]
    [InlineData("123", "***")]
    public void Phone_mask_keeps_only_the_last_four_digits(string phone, string expected)
    {
        Assert.Equal(expected, LogMask.Phone(phone));
    }

    [Theory]
    [InlineData("OTP for +919876543210 failed", "OTP for ********3210 failed")]
    [InlineData("phone 9876543210.", "phone ******3210.")]
    [InlineData("card 4111 1111 1111 1111 declined", "card ************1111 declined")]
    [InlineData("card 4111111111111111", "card ************1111")]
    [InlineData("job 34fc2c8e-3ff2-49a6-acac-480ecba21234 at 2026-10-14 08:27:47", "job 34fc2c8e-3ff2-49a6-acac-480ecba21234 at 2026-10-14 08:27:47")]
    [InlineData("payment pay_2912345678901 for 799.00", "payment pay_2912345678901 for 799.00")]
    public void Text_mask_hides_phone_and_card_numbers_but_not_ids_or_dates(string text, string expected)
    {
        Assert.Equal(expected, LogMask.Text(text));
    }

    [Fact]
    public async Task Flag_off_sends_nothing_even_for_open_errors()
    {
        await using var factory = new YogaApiFactory();
        var (host, sent) = WithRecordingSender(factory);
        await using var _ = host;
        var now = DateTimeOffset.UtcNow;
        await AddErrorAsync(host, "Type.A", now);

        Assert.Equal(0, await RunAlertsAsync(host, now));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task Flag_on_sends_one_alert_per_error_type_per_throttle_window()
    {
        await using var factory = new YogaApiFactory();
        var (host, sent) = WithRecordingSender(factory);
        await using var _ = host;
        await PatchSettingsAsync(await SignInAsync(host), new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail, errorAlertThrottleMinutes = 30 });
        var now = DateTimeOffset.UtcNow;

        await AddErrorAsync(host, "Type.A", now);
        Assert.Equal(1, await RunAlertsAsync(host, now));

        await AddErrorAsync(host, "Type.A", now.AddMinutes(5));
        await AddErrorAsync(host, "Type.B", now.AddMinutes(5));
        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(10)));
        Assert.Equal(0, await RunAlertsAsync(host, now.AddMinutes(20)));

        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(31)));
        Assert.Equal(0, await RunAlertsAsync(host, now.AddMinutes(70)));

        Assert.Equal(["Type.A", "Type.B", "Type.A"], sent.Select(m => m.Subject.Split(": ", 2)[1]));
        Assert.All(sent, m => Assert.Equal(AdminEmail, m.To));
    }

    [Fact]
    public async Task Missing_smtp_settings_fall_back_to_the_no_op_sender_and_never_throw()
    {
        await using var factory = new YogaApiFactory();
        Assert.IsType<NoopAlertSender>(factory.Services.GetRequiredService<IAlertSender>());

        var admin = await SignInAsync(factory);
        await PatchSettingsAsync(admin, new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail });
        var now = DateTimeOffset.UtcNow;
        await AddErrorAsync(factory, "Type.A", now);
        Assert.Equal(1, await RunAlertsAsync(factory, now));

        var test = await admin.PostAsync("/api/admin/errors/test-alert", null);
        Assert.Equal(HttpStatusCode.Conflict, test.StatusCode);

        await using var smtp = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerts:Smtp:Host"] = "smtp.example.com",
                ["Alerts:Smtp:From"] = "alerts@example.com"
            })));
        Assert.IsType<SmtpAlertSender>(smtp.Services.GetRequiredService<IAlertSender>());
    }

    [Fact]
    public void Startup_warns_once_when_alert_emails_cannot_go_out()
    {
        Assert.Contains("no-op", AlertSenders.StartupWarning(smtpConfigured: false, emailAlertsEnabled: true));
        Assert.Contains("off in admin settings", AlertSenders.StartupWarning(smtpConfigured: true, emailAlertsEnabled: false));
        Assert.Null(AlertSenders.StartupWarning(smtpConfigured: true, emailAlertsEnabled: true));
    }

    [Fact]
    public async Task Test_email_works_only_when_the_flag_is_on()
    {
        await using var factory = new YogaApiFactory();
        var (host, sent) = WithRecordingSender(factory);
        await using var _ = host;
        var admin = await SignInAsync(host);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/admin/errors/test-alert", null)).StatusCode);
        Assert.Empty(sent);

        await PatchSettingsAsync(admin, new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail });
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/admin/errors/test-alert", null)).StatusCode);
        Assert.Equal(AdminEmail, Assert.Single(sent).To);
    }

    [Fact]
    public async Task Turning_alerts_on_needs_an_email_and_every_change_is_audited()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory);

        var current = await admin.GetFromJsonAsync<VersionBody>("/api/admin/settings", Json);
        var refused = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = current!.Version, emailAlertsEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var badEmail = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = current.Version, errorAlertEmail = "not an email" });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);

        await PatchSettingsAsync(admin, new
        {
            emailAlertsEnabled = true,
            errorAlertEmail = AdminEmail,
            errorAlertThrottleMinutes = 15,
            errorAlertCheckMinutes = 2,
            errorEventRetentionDays = 7
        });

        var audit = await admin.GetFromJsonAsync<List<AuditBody>>("/api/admin/settings/audit", Json);
        Assert.Contains(audit!, a => a is { Field: "EmailAlertsEnabled", OldValue: "False", NewValue: "True" });
        Assert.Contains(audit!, a => a is { Field: "ErrorAlertEmail", NewValue: AdminEmail });
        Assert.Contains(audit!, a => a is { Field: "ErrorAlertThrottleMinutes", NewValue: "15" });
        Assert.Contains(audit!, a => a is { Field: "ErrorAlertCheckMinutes", NewValue: "2" });
        Assert.Contains(audit!, a => a is { Field: "ErrorEventRetentionDays", NewValue: "7" });
    }

    [Fact]
    public async Task A_critical_log_is_stored_masked_and_sends_exactly_one_alert_to_the_admin_email()
    {
        await using var factory = new YogaApiFactory();
        var (host, sent) = WithRecordingSender(factory);
        await using var _ = host;
        var admin = await SignInAsync(host);
        await PatchSettingsAsync(admin, new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail, errorAlertThrottleMinutes = 60 });

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Tests.Alerts");
        using (logger.BeginScope(new Dictionary<string, object> { [CorrelationIdMiddleware.LogProperty] = "corr-critical-1" }))
        {
            logger.LogCritical("Payout export broke for {Phone}", "+919876543210");
            logger.LogCritical("Payout export broke for {Phone}", "+919876543210");
        }
        var error = await WaitForErrorAsync(host, "Tests.Alerts: Payout export broke for {Phone}", count: 2);
        Assert.Equal("Critical", error.Level);
        Assert.Equal("corr-critical-1", error.LastCorrelationId);
        Assert.DoesNotContain("9876543210", error.LastMessage);
        Assert.Contains("3210", error.LastMessage);

        Assert.Equal(1, await RunAlertsAsync(host, DateTimeOffset.UtcNow));
        Assert.Equal(0, await RunAlertsAsync(host, DateTimeOffset.UtcNow.AddMinutes(1)));
        var alert = Assert.Single(sent);
        Assert.Equal(AdminEmail, alert.To);
        Assert.Contains("Critical", alert.Subject);
        Assert.DoesNotContain("9876543210", alert.Body);
    }

    [Fact]
    public async Task Admin_lists_and_acknowledges_errors_and_the_open_count_drops()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory);
        var now = DateTimeOffset.UtcNow;
        var id = await AddErrorAsync(factory, "Type.A", now);
        await AddErrorAsync(factory, "Type.B", now.AddMinutes(1));
        var open = (await admin.GetFromJsonAsync<SummaryBody>("/api/admin/errors/summary", Json))!.Unacknowledged;

        var list = await admin.GetFromJsonAsync<ErrorPage>("/api/admin/errors?pageSize=50", Json);
        Assert.Equal(open, list!.Total);
        Assert.Contains(list.Items, e => e.Id == id && e.ErrorType == "Type.A" && e.Count == 1);

        var acknowledged = await admin.PostAsync($"/api/admin/errors/{id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        Assert.Equal(open - 1, (await admin.GetFromJsonAsync<SummaryBody>("/api/admin/errors/summary", Json))!.Unacknowledged);
        var done = await admin.GetFromJsonAsync<ErrorPage>("/api/admin/errors?acknowledged=true", Json);
        Assert.Contains(done!.Items, e => e.Id == id && e.AcknowledgedAt != null);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/admin/errors/{Guid.NewGuid()}/acknowledge", null)).StatusCode);
    }

    private static (WebApplicationFactory<Program> Host, ConcurrentQueue<SentAlert> Sent) WithRecordingSender(YogaApiFactory factory)
    {
        var sent = new ConcurrentQueue<SentAlert>();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IAlertSender>(new RecordingAlertSender(sent)))));
        return (host, sent);
    }

    private static async Task<Guid> AddErrorAsync(WebApplicationFactory<Program> host, string type, DateTimeOffset at)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var row = await db.ErrorEvents.SingleOrDefaultAsync(e => e.ErrorType == type);
        if (row is null)
        {
            row = new ErrorEvent { Id = Guid.NewGuid(), ErrorType = type, Level = "Error", LastMessage = "Boom", FirstSeenAt = at };
            db.ErrorEvents.Add(row);
        }
        row.Count++;
        row.LastSeenAt = at;
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<ErrorEvent> WaitForErrorAsync(WebApplicationFactory<Program> host, string type, int count)
    {
        for (var i = 0; i < 100; i++)
        {
            using var scope = host.Services.CreateScope();
            var row = await scope.ServiceProvider.GetRequiredService<YogaDbContext>().ErrorEvents.AsNoTracking()
                .SingleOrDefaultAsync(e => e.ErrorType == type);
            if (row?.Count >= count)
                return row;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Error type '{type}' was not recorded {count} times.");
    }

    private static async Task<int> RunAlertsAsync(WebApplicationFactory<Program> host, DateTimeOffset now)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ErrorAlertJob>().RunAsync(now, CancellationToken.None);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? correlationId, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (correlationId is not null)
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }

    private static string Header(HttpResponseMessage response) =>
        string.Join(",", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));

    private static async Task<string?> BodyIdAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))?.CorrelationId;

    private static async Task PatchSettingsAsync(HttpClient admin, object changes)
    {
        var current = await admin.GetFromJsonAsync<VersionBody>("/api/admin/settings", Json);
        var body = JsonSerializer.SerializeToNode(changes, Json)!.AsObject();
        body["version"] = current!.Version;
        var saved = await admin.PatchAsJsonAsync("/api/admin/settings", body, Json);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var otp = await (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = SeedIds.AdminPhone, isNewUser = false }))
            .Content.ReadFromJsonAsync<OtpBody>(Json);
        var auth = await (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone = SeedIds.AdminPhone, code = otp!.DevCode }))
            .Content.ReadFromJsonAsync<AuthBody>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private sealed record SentAlert(string To, string Subject, string Body);

    private sealed class RecordingAlertSender(ConcurrentQueue<SentAlert> sent) : IAlertSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            sent.Enqueue(new SentAlert(to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record VersionBody(int Version);
    private sealed record ErrorBody(string Error, string? CorrelationId);
    private sealed record AuditBody(string Field, string? OldValue, string? NewValue);
    private sealed record SummaryBody(int Unacknowledged);
    private sealed record ErrorRow(Guid Id, string ErrorType, int Count, DateTimeOffset? AcknowledgedAt);
    private sealed record ErrorPage(List<ErrorRow> Items, int Total);
}

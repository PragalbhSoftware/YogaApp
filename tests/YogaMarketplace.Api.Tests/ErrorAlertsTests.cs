using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Logging;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Alert tests get their own database: the alert job reads every open error in it. Hosts built here have no background
/// error writer; tests drain the error queue themselves, so nothing waits on timing.
/// </summary>
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

    [Theory]
    [InlineData("alert to ops.team+errors@example.co.in failed", "alert to ***@example.co.in failed")]
    [InlineData("From: Meera_Rao99@mail.example.com.", "From: ***@mail.example.com.")]
    [InlineData("no address @ here or user@localhost", "no address @ here or user@localhost")]
    public void Text_mask_keeps_only_the_domain_of_an_email(string text, string expected)
    {
        Assert.Equal(expected, LogMask.Text(text));
    }

    [Theory]
    [InlineData("Authorization: Bearer abc.DEF-123_x~+/=", "Authorization: Bearer [token]")]
    [InlineData("header bearer opaque-refresh-token-42 rejected", "header Bearer [token] rejected")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.sig-Part_1 expired", "token [jwt] expired")]
    [InlineData("unsigned eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0. used", "unsigned [jwt] used")]
    public void Text_mask_replaces_bearer_tokens_and_jwts(string text, string expected)
    {
        Assert.Equal(expected, LogMask.Text(text));
    }

    [Fact]
    public async Task Error_endpoints_are_admin_only()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await using var _ = host;
        await PatchSettingsAsync(await SignInAsync(host, SeedIds.AdminPhone), new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail });
        var id = await AddErrorAsync(host, "Type.A", DateTimeOffset.UtcNow);
        var callers = new (HttpClient Client, HttpStatusCode Expected)[]
        {
            (host.CreateClient(), HttpStatusCode.Unauthorized),
            (await SignUpAsync(host), HttpStatusCode.Forbidden),
            (await SignInAsync(host, SeedIds.AnanyaPhone), HttpStatusCode.Forbidden)
        };

        foreach (var (client, expected) in callers)
        {
            Assert.Equal(expected, (await client.GetAsync("/api/admin/errors")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/admin/errors/summary")).StatusCode);
            Assert.Equal(expected, (await client.PostAsync($"/api/admin/errors/{id}/acknowledge", null)).StatusCode);
            Assert.Equal(expected, (await client.PostAsync("/api/admin/errors/test-alert", null)).StatusCode);
        }

        Assert.Empty(sender.Sent);
        Assert.Null((await ErrorRowAsync(host, "Type.A"))!.AcknowledgedAt);
    }

    [Fact]
    public async Task Flag_off_sends_nothing_even_for_open_errors()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await using var _ = host;
        var now = DateTimeOffset.UtcNow;
        await AddErrorAsync(host, "Type.A", now);

        Assert.Equal(0, await RunAlertsAsync(host, now));
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Flag_on_sends_one_alert_per_error_type_per_throttle_window()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await using var _ = host;
        await PatchSettingsAsync(await SignInAsync(host, SeedIds.AdminPhone), new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail, errorAlertThrottleMinutes = 30 });
        var now = DateTimeOffset.UtcNow;

        await AddErrorAsync(host, "Type.A", now);
        Assert.Equal(1, await RunAlertsAsync(host, now));

        await AddErrorAsync(host, "Type.A", now.AddMinutes(5));
        await AddErrorAsync(host, "Type.B", now.AddMinutes(5));
        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(10)));
        Assert.Equal(0, await RunAlertsAsync(host, now.AddMinutes(20)));

        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(31)));
        Assert.Equal(0, await RunAlertsAsync(host, now.AddMinutes(70)));

        Assert.Equal(["Type.A", "Type.B", "Type.A"], sender.Sent.Select(m => m.Subject.Split(": ", 2)[1]));
        Assert.All(sender.Sent, m => Assert.Equal(AdminEmail, m.To));
    }

    [Fact]
    public async Task An_smtp_error_is_caught_the_other_alerts_still_go_out_and_the_failed_one_is_retried()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, logs) = NewHost(factory);
        await using var _ = host;
        await PatchSettingsAsync(await SignInAsync(host, SeedIds.AdminPhone), new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail });
        var now = DateTimeOffset.UtcNow;
        await AddErrorAsync(host, "Type.A", now);
        await AddErrorAsync(host, "Type.B", now.AddSeconds(1));
        sender.FailFor = "Type.A";

        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(1)));
        Assert.EndsWith("Type.B", Assert.Single(sender.Sent).Subject);
        Assert.Null((await ErrorRowAsync(host, "Type.A"))!.LastAlertedAt);
        Assert.Contains(logs.Lines, l => l.StartsWith("Warning Could not send the error alert for \"Type.A\"", StringComparison.Ordinal));

        sender.FailFor = null;
        Assert.Equal(1, await RunAlertsAsync(host, now.AddMinutes(2)));
        Assert.EndsWith("Type.A", sender.Sent.Last().Subject);
    }

    [Fact]
    public async Task Clean_up_deletes_only_errors_last_seen_before_the_retention_period()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, _) = NewHost(factory);
        await using var _ = host;
        await PatchSettingsAsync(await SignInAsync(host, SeedIds.AdminPhone), new { errorEventRetentionDays = 7 });
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-7);
        await AddErrorAsync(host, "Old", cutoff.AddSeconds(-1));
        await AddErrorAsync(host, "AtCutoff", cutoff);
        await AddErrorAsync(host, "Recent", now.AddDays(-6));

        await RunAlertsAsync(host, now);

        Assert.Null(await ErrorRowAsync(host, "Old"));
        Assert.NotNull(await ErrorRowAsync(host, "AtCutoff"));
        Assert.NotNull(await ErrorRowAsync(host, "Recent"));
    }

    [Fact]
    public async Task Missing_smtp_settings_fall_back_to_the_no_op_sender_and_never_throw()
    {
        await using var factory = new YogaApiFactory();
        Assert.IsType<NoopAlertSender>(factory.Services.GetRequiredService<IAlertSender>());

        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
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
        var (host, sender, _) = NewHost(factory);
        await using var _ = host;
        var admin = await SignInAsync(host, SeedIds.AdminPhone);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/admin/errors/test-alert", null)).StatusCode);
        Assert.Empty(sender.Sent);

        await PatchSettingsAsync(admin, new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail });
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/admin/errors/test-alert", null)).StatusCode);
        Assert.Equal(AdminEmail, Assert.Single(sender.Sent).To);
    }

    [Fact]
    public async Task Turning_alerts_on_needs_an_email_and_every_change_is_audited()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);

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
        var (host, sender, _) = NewHost(factory);
        await using var _ = host;
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        await PatchSettingsAsync(admin, new { emailAlertsEnabled = true, errorAlertEmail = AdminEmail, errorAlertThrottleMinutes = 60 });

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Tests.Alerts");
        using (logger.BeginScope(new Dictionary<string, object> { [CorrelationIdMiddleware.LogProperty] = "corr-critical-1" }))
        {
            logger.LogCritical("Payout export broke for {Phone}", "+919876543210");
            logger.LogCritical("Payout export broke for {Phone}", "+919876543210");
        }
        await DrainErrorsAsync(host);
        var error = (await ErrorRowAsync(host, "Tests.Alerts: Payout export broke for {Phone}"))!;
        Assert.Equal(2, error.Count);
        Assert.Equal("Critical", error.Level);
        Assert.Equal("corr-critical-1", error.LastCorrelationId);
        Assert.DoesNotContain("9876543210", error.LastMessage);
        Assert.Contains("3210", error.LastMessage);

        Assert.Equal(1, await RunAlertsAsync(host, DateTimeOffset.UtcNow));
        Assert.Equal(0, await RunAlertsAsync(host, DateTimeOffset.UtcNow.AddMinutes(1)));
        var alert = Assert.Single(sender.Sent);
        Assert.Equal(AdminEmail, alert.To);
        Assert.Contains("Critical", alert.Subject);
        Assert.DoesNotContain("9876543210", alert.Body);
    }

    [Fact]
    public async Task The_same_error_type_counts_on_one_row_and_a_different_type_gets_its_own()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, _) = NewHost(factory);
        await using var _ = host;
        var writer = NewWriter(host);
        var first = DateTimeOffset.UtcNow;

        await writer.WriteAsync(Occurrence("Type.A", "corr-a1", first), CancellationToken.None);
        await writer.WriteAsync(Occurrence("Type.A", "corr-a2", first.AddMinutes(1)), CancellationToken.None);
        await writer.WriteAsync(Occurrence("Type.B", "corr-b1", first.AddMinutes(2)), CancellationToken.None);

        var a = (await ErrorRowAsync(host, "Type.A"))!;
        var b = (await ErrorRowAsync(host, "Type.B"))!;
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal((2, "corr-a2", first), (a.Count, a.LastCorrelationId, a.FirstSeenAt));
        Assert.Equal((1, "corr-b1"), (b.Count, b.LastCorrelationId));
    }

    [Fact]
    public async Task An_acknowledged_error_seen_again_reopens_the_same_row_with_a_fresh_count()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, _) = NewHost(factory);
        await using var _ = host;
        var writer = NewWriter(host);
        var again = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.Zero);
        var first = again.AddHours(-2);
        await writer.WriteAsync(Occurrence("Type.A", "corr-old-1", first), CancellationToken.None);
        await writer.WriteAsync(Occurrence("Type.A", "corr-old-2", first.AddMinutes(1)), CancellationToken.None);
        var before = (await ErrorRowAsync(host, "Type.A"))!;
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/errors/{before.Id}/acknowledge", null)).StatusCode);
        Assert.NotNull((await ErrorRowAsync(host, "Type.A"))!.AcknowledgedAt);

        await writer.WriteAsync(Occurrence("Type.A", "corr-new", again), CancellationToken.None);

        var after = (await ErrorRowAsync(host, "Type.A"))!;
        Assert.Equal(before.Id, after.Id);
        Assert.Null(after.AcknowledgedAt);
        Assert.Null(after.AcknowledgedByUserId);
        Assert.Equal((1, again, again, "corr-new"), (after.Count, after.FirstSeenAt, after.LastSeenAt, after.LastCorrelationId));
    }

    [Fact]
    public async Task Client_errors_400_401_403_and_409_create_no_error_rows()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, _) = NewHost(factory);
        await using var _ = host;
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        var customer = await SignUpAsync(host);
        await DrainErrorsAsync(host);

        var responses = new[]
        {
            await SendAsync(host.CreateClient(), HttpMethod.Post, "/api/auth/otp/request", "corr-400", new { phone = "123", isNewUser = false }),
            await SendAsync(host.CreateClient(), HttpMethod.Get, "/api/admin/errors", "corr-401"),
            await SendAsync(customer, HttpMethod.Get, "/api/admin/errors", "corr-403"),
            await SendAsync(admin, HttpMethod.Post, "/api/admin/errors/test-alert", "corr-409")
        };

        Assert.Equal(
            [HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode));
        Assert.Equal(0, await DrainErrorsAsync(host));
        Assert.False(await AnyErrorAsync(host, e => e.LastCorrelationId != null && e.LastCorrelationId.StartsWith("corr-4")));
    }

    [Fact]
    public async Task A_5xx_is_recorded_a_4xx_is_not_and_a_failed_error_write_still_returns_500()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, logs) = NewHost(factory, brokenContentPages: true);
        await using var _ = host;
        var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/api/no-such-endpoint", "corr-404")).StatusCode);
        await DrainErrorsAsync(host);
        Assert.False(await AnyErrorAsync(host, e => e.LastCorrelationId == "corr-404"));

        Assert.Equal(HttpStatusCode.InternalServerError, (await SendAsync(client, HttpMethod.Get, "/api/pages/terms", "corr-500")).StatusCode);
        await DrainErrorsAsync(host);
        Assert.True(await AnyErrorAsync(host, e => e.LastCorrelationId == "corr-500"));

        using (var scope = host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<YogaDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE ErrorEvents");

        var failed = await SendAsync(client, HttpMethod.Get, "/api/pages/terms", "corr-500-no-table");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var body = await failed.Content.ReadFromJsonAsync<ErrorBody>(Json);
        Assert.Equal((ApiErrorResponses.UnexpectedMessage, "corr-500-no-table"), (body!.Error, body.CorrelationId));
        Assert.True(await DrainErrorsAsync(host) > 0);
        Assert.Contains(logs.Lines, l => l.Contains("Could not record error event", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logs_never_contain_otp_codes_phones_emails_tokens_secrets_passwords_headers_bodies_or_query_tokens()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, logs) = NewHost(factory, brokenContentPages: true);
        await using var _ = host;
        const string razorpaySecret = "rzp-live-secret-in-logs-test";
        const string password = "body-password-secret";
        const string queryToken = "query-token-secret";
        const string cookie = "cookie-secret-value";

        var anonymous = host.CreateClient();
        var phone = "+9196" + Random.Shared.Next(10000000, 99999999);
        var otp = await (await anonymous.PostAsJsonAsync("/api/auth/otp/request",
            new { phone, name = "Log Check", gender = "Female", isNewUser = true, acceptedPolicies = true })).Content.ReadFromJsonAsync<OtpBody>(Json);
        var wrongCode = otp!.DevCode == "000000" ? "111111" : "000000";
        var refused = await anonymous.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = wrongCode, password });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var auth = await (await anonymous.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = otp.DevCode })).Content.ReadFromJsonAsync<AuthBody>(Json);

        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        await PatchSettingsAsync(admin, new { errorAlertEmail = AdminEmail });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/pages/terms?token={queryToken}&access_token={queryToken}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        request.Headers.Add("Cookie", $"refresh={cookie}");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "corr-logs");
        Assert.Equal(HttpStatusCode.InternalServerError, (await anonymous.SendAsync(request)).StatusCode);

        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"rzp_test_logs:{razorpaySecret}"));
        var razorpay = new RazorpayHttpClient(
            new HttpClient(new EchoingRazorpayError(razorpaySecret)) { BaseAddress = new Uri("https://api.razorpay.test/") },
            Microsoft.Extensions.Options.Options.Create(new RazorpayOptions { KeyId = "rzp_test_logs", KeySecret = razorpaySecret }),
            host.Services.GetRequiredService<ILogger<RazorpayHttpClient>>());
        await Assert.ThrowsAsync<DomainException>(() => razorpay.RefundPaymentAsync("pay_logs", 100, "rcpt_logs", CancellationToken.None));

        var all = string.Join('\n', logs.Lines);
        var unhandled = Assert.Single(logs.Lines, l => l.StartsWith("Error An unhandled exception", StringComparison.Ordinal));
        Assert.Contains("CorrelationId=corr-logs", unhandled);
        Assert.Contains("RequestPath=/api/pages/terms ", unhandled);
        Assert.Contains("RequestId=", unhandled);
        Assert.Contains("Razorpay \"refund payment\" failed with 400: \"BAD_REQUEST_ERROR\"", all);
        Assert.DoesNotMatch($"(?<![0-9A-Za-z]){Regex.Escape(otp.DevCode)}(?![0-9A-Za-z])", all);
        Assert.DoesNotMatch($"(?<![0-9A-Za-z]){Regex.Escape(wrongCode)}(?![0-9A-Za-z])", all);
        foreach (var secret in new[]
                 {
                     phone[3..], SeedIds.AdminPhone[3..], AdminEmail, auth.Token, admin.DefaultRequestHeaders.Authorization!.Parameter!,
                     razorpaySecret, basic, password, queryToken, cookie, "Bearer ", "Basic ", "refresh="
                 })
            Assert.DoesNotContain(secret, all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_lists_and_acknowledges_errors_and_the_open_count_drops()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
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

    /// <summary>A host with a recording alert sender, captured logs and no background error writer.</summary>
    private static (WebApplicationFactory<Program> Host, RecordingAlertSender Sender, LogCapture Logs) NewHost(
        YogaApiFactory factory, bool brokenContentPages = false)
    {
        var sender = new RecordingAlertSender();
        var logs = new LogCapture();
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IAlertSender>(sender));
                services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ErrorEventWriter)));
                if (brokenContentPages)
                    services.AddScoped<ContentPageService>(_ => throw new InvalidOperationException("Content store is down."));
            });
        });
        return (host, sender, logs);
    }

    private static ErrorEventWriter NewWriter(WebApplicationFactory<Program> host) =>
        ActivatorUtilities.CreateInstance<ErrorEventWriter>(host.Services);

    /// <summary>Writes every queued error the way the background writer would. Returns how many there were.</summary>
    private static async Task<int> DrainErrorsAsync(WebApplicationFactory<Program> host)
    {
        var queue = host.Services.GetRequiredService<ErrorEventQueue>();
        var writer = NewWriter(host);
        var written = 0;
        while (queue.Reader.TryRead(out var occurrence))
        {
            await writer.WriteAsync(occurrence, CancellationToken.None);
            written++;
        }
        return written;
    }

    private static ErrorOccurrence Occurrence(string type, string correlationId, DateTimeOffset at) =>
        new(type, "Error", "Tests", "Boom", correlationId, at);

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

    private static async Task<ErrorEvent?> ErrorRowAsync(WebApplicationFactory<Program> host, string type)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<YogaDbContext>().ErrorEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.ErrorType == type);
    }

    private static async Task<bool> AnyErrorAsync(WebApplicationFactory<Program> host, System.Linq.Expressions.Expression<Func<ErrorEvent, bool>> match)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<YogaDbContext>().ErrorEvents.AnyAsync(match);
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

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string phone)
    {
        var client = host.CreateClient();
        var otp = await (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone, isNewUser = false }))
            .Content.ReadFromJsonAsync<OtpBody>(Json);
        return await VerifyAsync(client, phone, otp!.DevCode);
    }

    private static async Task<HttpClient> SignUpAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var phone = "+9197" + Random.Shared.Next(10000000, 99999999);
        var otp = await (await client.PostAsJsonAsync("/api/auth/otp/request",
            new { phone, name = "Error Page Customer", gender = "Female", isNewUser = true, acceptedPolicies = true })).Content.ReadFromJsonAsync<OtpBody>(Json);
        return await VerifyAsync(client, phone, otp!.DevCode);
    }

    private static async Task<HttpClient> VerifyAsync(HttpClient client, string phone, string code)
    {
        var auth = await (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<AuthBody>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private sealed record SentAlert(string To, string Subject, string Body);

    /// <summary>Records alerts. <see cref="FailFor"/> makes alerts whose subject ends with that error type fail like an SMTP outage.</summary>
    private sealed class RecordingAlertSender : IAlertSender
    {
        public ConcurrentQueue<SentAlert> Sent { get; } = new();
        public string? FailFor { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            if (FailFor is not null && subject.EndsWith(FailFor, StringComparison.Ordinal))
                throw new SmtpException(SmtpStatusCode.ServiceNotAvailable, "Mailbox unavailable.");
            Sent.Enqueue(new SentAlert(to, subject, body));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Every event Serilog lets through, as "level message | properties | exception". Serilog folds logger scopes
    /// (CorrelationId, RequestId, RequestPath, ActionName, ...) into the event properties, so they are captured too.
    /// </summary>
    private sealed class LogCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Lines);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? string.Join(" ", values.Select(p => $"{p.Key}={p.Value}"))
                    : "";
                lines.Enqueue($"{logLevel} {formatter(state, exception)} | {properties} | {exception}");
            }
        }
    }

    /// <summary>A Razorpay error whose body echoes the key secret and the Authorization header back.</summary>
    private sealed class EchoingRazorpayError(string secret) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var echo = JsonSerializer.Serialize(new
            {
                error = new { code = "BAD_REQUEST_ERROR", description = "The payment id is invalid." },
                debug = new { secret, authorization = request.Headers.Authorization?.ToString() }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(echo, Encoding.UTF8, "application/json") });
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

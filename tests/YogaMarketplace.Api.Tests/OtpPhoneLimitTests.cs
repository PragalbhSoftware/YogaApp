using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class OtpPhoneLimitTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Requesting_a_code_answers_the_same_for_new_registered_blocked_and_locked_phones()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var host = Host(factory, sender);
        var registered = await SignUpAsync(host, sender);
        var blocked = await SignUpAsync(host, sender);
        var locked = NewPhone();
        var unknown = NewPhone();
        var signup = NewPhone();
        await WithDbAsync(host, async db =>
        {
            await db.Users.Where(u => u.Phone == blocked).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsBlocked, true));
            db.OtpPhoneLocks.Add(new OtpPhoneLock { Phone = locked, LockedUntil = DateTimeOffset.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();
        });
        sender.Sent.Clear();

        var client = host.CreateClient();
        var shapes = new List<string>();
        foreach (var (phone, isNewUser) in new[]
                 {
                     (unknown, false), (signup, true), (registered, false), (registered, true),
                     (blocked, false), (blocked, true), (locked, false), (locked, true)
                 })
        {
            var response = await client.PostAsJsonAsync("/api/auth/otp/request", NewUserBody(phone, isNewUser), Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            shapes.Add(string.Join(",", body.Select(p => $"{p.Key}:{p.Value?.GetValueKind().ToString() ?? "null"}")));
        }

        Assert.StartsWith("challengeId:String,expiresAt:String", Assert.Single(shapes.Distinct()));
        Assert.Equal(new[] { signup, registered, registered }, sender.Sent.Select(s => s.Phone).ToArray());
    }

    [Fact]
    public async Task A_wrong_code_answers_the_same_for_unknown_and_registered_phones()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var host = Host(factory, sender);
        var registered = await SignUpAsync(host, sender);
        var unknown = NewPhone();
        var client = host.CreateClient();

        await RequestAsync(client, registered);
        await RequestAsync(client, unknown);
        var wrong = sender.LastCodeFor(registered) == "000000" ? "111111" : "000000";
        var forRegistered = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone = registered, code = wrong });
        var forUnknown = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone = unknown, code = wrong });

        Assert.Equal(HttpStatusCode.BadRequest, forRegistered.StatusCode);
        Assert.Equal(forRegistered.StatusCode, forUnknown.StatusCode);
        Assert.Equal(await ErrorAsync(forRegistered), await ErrorAsync(forUnknown));
    }

    [Fact]
    public async Task Requests_over_the_send_limit_lock_the_phone_and_log_it_masked()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var logs = new LogCapture();
        var host = Host(factory, sender, logs);
        await SetLimitsAsync(host, sender, version => new { version, otpMaxSendsPerPhone = 2, otpLockMinutes = 10 });
        var phone = NewPhone();
        var client = host.CreateClient();

        await RequestAsync(client, phone, isNewUser: true);
        await RequestAsync(client, phone, isNewUser: true);
        var lastCode = sender.LastCodeFor(phone);
        await RequestAsync(client, phone, isNewUser: true);

        Assert.Equal(2, sender.Sent.Count(s => s.Phone == phone));
        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = lastCode });
        Assert.Equal(HttpStatusCode.TooManyRequests, verify.StatusCode);
        Assert.Equal(OtpService.LockedMessage, await ErrorAsync(verify));
        var lockedUntil = await WithDbAsync(host, db => db.OtpPhoneLocks.AsNoTracking().SingleAsync(l => l.Phone == phone));
        Assert.InRange(lockedUntil.LockedUntil - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));

        var warning = Assert.Single(logs.Lines, line => line.Contains("Locked sign-in codes", StringComparison.Ordinal));
        Assert.StartsWith("Warning", warning);
        Assert.Contains(phone[^4..], warning);
        Assert.DoesNotContain(phone[3..], warning);
    }

    [Fact]
    public async Task Wrong_codes_over_the_limit_lock_the_phone_across_resends()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var host = Host(factory, sender);
        await SetLimitsAsync(host, sender, version => new { version, otpMaxWrongCodesPerPhone = 3, otpLockMinutes = 10 });
        var phone = await SignUpAsync(host, sender);
        var client = host.CreateClient();

        await RequestAsync(client, phone);
        var wrong = WrongCode(sender.LastCodeFor(phone));
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, phone, wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, phone, wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/otp/resend", new { phone })).StatusCode);
        var fresh = sender.LastCodeFor(phone);

        var third = await VerifyAsync(client, phone, WrongCode(fresh));
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await VerifyAsync(client, phone, fresh)).StatusCode);
        var sentBefore = sender.Sent.Count;
        await RequestAsync(client, phone);
        Assert.Equal(sentBefore, sender.Sent.Count);
    }

    [Fact]
    public async Task A_locked_phone_can_sign_in_again_once_the_lock_minutes_pass()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var clock = new ShiftedClock();
        var host = Host(factory, sender, clock: clock);
        await SetLimitsAsync(host, sender, version => new { version, otpMaxSendsPerPhone = 1, otpLockMinutes = 10 });
        var phone = NewPhone();
        var client = host.CreateClient();

        await RequestAsync(client, phone, isNewUser: true);
        await RequestAsync(client, phone, isNewUser: true);
        clock.Offset = TimeSpan.FromMinutes(9);
        await RequestAsync(client, phone, isNewUser: true);
        Assert.Equal(1, sender.Sent.Count(s => s.Phone == phone));

        clock.Offset = TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1);
        await RequestAsync(client, phone, isNewUser: true);

        Assert.Equal(2, sender.Sent.Count(s => s.Phone == phone));
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(client, phone, sender.LastCodeFor(phone))).StatusCode);
    }

    [Theory]
    [InlineData("otpMaxSendsPerPhone", 0)]
    [InlineData("otpMaxSendsPerPhone", 21)]
    [InlineData("otpMaxWrongCodesPerPhone", 0)]
    [InlineData("otpMaxWrongCodesPerPhone", 51)]
    [InlineData("otpLockMinutes", 0)]
    [InlineData("otpLockMinutes", 1441)]
    public async Task Otp_limit_settings_outside_the_range_are_rejected(string field, int value)
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var admin = await SignInAsync(Host(factory, sender), sender, SeedIds.AdminPhone);
        var version = (await AdminSettingsClient.GetAsync(admin)).Version;

        var response = await admin.PatchAsJsonAsync("/api/admin/settings", new JsonObject { ["version"] = version, [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(version, (await AdminSettingsClient.GetAsync(admin)).Version);
    }

    [Theory]
    [InlineData("otpMaxSendsPerPhone", 1)]
    [InlineData("otpMaxSendsPerPhone", 20)]
    [InlineData("otpMaxWrongCodesPerPhone", 1)]
    [InlineData("otpMaxWrongCodesPerPhone", 50)]
    [InlineData("otpLockMinutes", 1)]
    [InlineData("otpLockMinutes", 1440)]
    public async Task Otp_limit_settings_accept_the_range_ends_and_are_audited(string field, int value)
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var host = Host(factory, sender);
        var admin = await SignInAsync(host, sender, SeedIds.AdminPhone);

        await AdminSettingsClient.PatchAsync(admin, version => new JsonObject { ["version"] = version, [field] = value });

        var saved = JsonNode.Parse(await admin.GetStringAsync("/api/admin/settings"))!;
        Assert.Equal(value, saved[field]!.GetValue<int>());
        var audit = await admin.GetFromJsonAsync<List<AuditBody>>("/api/admin/settings/audit", Json);
        Assert.Contains(audit!, a => string.Equals(a.Field, field, StringComparison.OrdinalIgnoreCase)
            && a.NewValue == value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Saving_otp_limits_with_a_stale_version_is_a_conflict()
    {
        await using var factory = new YogaApiFactory();
        var sender = new RecordingOtpSender();
        var admin = await SignInAsync(Host(factory, sender), sender, SeedIds.AdminPhone);
        var stale = (await AdminSettingsClient.GetAsync(admin)).Version;
        await AdminSettingsClient.PatchAsync(admin, version => new { version, otpLockMinutes = 15 });

        var response = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = stale, otpLockMinutes = 45 }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(15, JsonNode.Parse(await admin.GetStringAsync("/api/admin/settings"))!["otpLockMinutes"]!.GetValue<int>());
    }

    private static WebApplicationFactory<Program> Host(
        YogaApiFactory factory, RecordingOtpSender sender, LogCapture? logs = null, TimeProvider? clock = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (logs is not null)
                builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.Configure<OtpOptions>(options => options.ExposeCode = false);
                services.Replace(ServiceDescriptor.Singleton<IOtpSender>(sender));
                if (clock is not null)
                    services.Replace(ServiceDescriptor.Singleton(clock));
            });
        });

    /// <summary>Puts real per-phone limits back; the test factory starts them effectively unlimited.</summary>
    private static async Task SetLimitsAsync(WebApplicationFactory<Program> host, RecordingOtpSender sender, Func<int, object> body)
    {
        var admin = await SignInAsync(host, sender, SeedIds.AdminPhone);
        await AdminSettingsClient.PatchAsync(admin, body);
    }

    private static async Task<string> SignUpAsync(WebApplicationFactory<Program> host, RecordingOtpSender sender)
    {
        var phone = NewPhone();
        var client = host.CreateClient();
        await RequestAsync(client, phone, isNewUser: true);
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(client, phone, sender.LastCodeFor(phone))).StatusCode);
        return phone;
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, RecordingOtpSender sender, string phone)
    {
        var client = host.CreateClient();
        await RequestAsync(client, phone);
        var response = await VerifyAsync(client, phone, sender.LastCodeFor(phone));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var token = JsonNode.Parse(payload)!["token"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task RequestAsync(HttpClient client, string phone, bool isNewUser = false)
    {
        var response = await client.PostAsJsonAsync("/api/auth/otp/request", NewUserBody(phone, isNewUser), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string phone, string code) =>
        client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code });

    private static object NewUserBody(string phone, bool isNewUser) => isNewUser
        ? new { phone, isNewUser, name = "Limit Tester", gender = "Female", acceptedPolicies = true }
        : new { phone, isNewUser };

    private static async Task<string> ErrorAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["error"]!.GetValue<string>();

    private static async Task WithDbAsync(WebApplicationFactory<Program> host, Func<YogaDbContext, Task> work)
    {
        using var scope = host.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static async Task<T> WithDbAsync<T>(WebApplicationFactory<Program> host, Func<YogaDbContext, Task<T>> work)
    {
        using var scope = host.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static string WrongCode(string code) => code == "000000" ? "111111" : "000000";

    private static string NewPhone() => "+9197" + Random.Shared.Next(10000000, 99999999);

    private sealed record AuditBody(string Field, string? OldValue, string? NewValue);

    private sealed class RecordingOtpSender : IOtpSender
    {
        public ConcurrentQueue<(string Phone, string Code)> Sent { get; } = new();

        public string LastCodeFor(string phone) => Sent.Last(s => s.Phone == phone).Code;

        public Task SendAsync(string phone, string code, CancellationToken cancellationToken)
        {
            Sent.Enqueue((phone, code));
            return Task.CompletedTask;
        }
    }

    private sealed class ShiftedClock : TimeProvider
    {
        public TimeSpan Offset { get; set; }

        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset;
    }

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

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue($"{logLevel} {formatter(state, exception)}");
        }
    }
}

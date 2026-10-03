using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class AuthRefreshTests : IClassFixture<YogaApiFactory>
{
    private const string CookieName = "ym_refresh";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public AuthRefreshTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sign_in_sets_an_http_only_refresh_cookie_and_refresh_rotates_it()
    {
        var session = await SignUpAsync("Refresh Rotation");
        var setCookie = session.SetCookie;
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        var refreshed = await RefreshAsync(session.Refresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        Assert.NotNull(refreshed.Body);
        Assert.NotEqual(session.Refresh, refreshed.Refresh);
        Assert.Equal(session.UserId, refreshed.Body!.User.Id);

        var me = Client(refreshed.Body.Token);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync("/api/auth/me")).StatusCode);

        var again = await RefreshAsync(refreshed.Refresh);
        Assert.Equal(HttpStatusCode.OK, again.Status);
    }

    [Fact]
    public async Task An_old_token_right_after_rotation_is_a_conflict_and_keeps_the_new_one_working()
    {
        var session = await SignUpAsync("Refresh Race");
        var first = await RefreshAsync(session.Refresh);

        var racing = await RefreshAsync(session.Refresh);
        Assert.Equal(HttpStatusCode.Conflict, racing.Status);
        Assert.Null(racing.Refresh);

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(first.Refresh)).Status);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_after_the_grace_revokes_the_whole_family()
    {
        var session = await SignUpAsync("Refresh Reuse");
        var current = await RefreshAsync(session.Refresh);
        await BackdateRevocationAsync(session.Refresh, TimeSpan.FromMinutes(5));

        var stolen = await RefreshAsync(session.Refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, stolen.Status);

        var legitimate = await RefreshAsync(current.Refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.Status);
        Assert.Equal(RefreshTokenRevokeReason.Reused, await RevokeReasonAsync(current.Refresh!));
    }

    [Fact]
    public async Task Missing_or_unknown_cookie_is_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(null)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("not-a-real-token")).Status);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_clears_the_cookie()
    {
        var session = await SignUpAsync("Refresh Logout");
        var rotated = await RefreshAsync(session.Refresh);

        var logout = await SendWithCookieAsync("/api/auth/logout", rotated.Refresh);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(CookieName + "=;", SetCookieOf(logout) ?? "", StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.Refresh)).Status);
        Assert.Equal(RefreshTokenRevokeReason.SignedOut, await RevokeReasonAsync(rotated.Refresh!));

        var withoutCookie = await SendWithCookieAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, withoutCookie.StatusCode);
    }

    [Fact]
    public async Task Blocking_revokes_refresh_tokens_and_the_next_request_is_unauthorized()
    {
        var session = await SignUpAsync("Refresh Blocked");
        var otherDevice = await SignInAsync(session.Phone);
        var customer = Client(session.Token);

        var admin = Client((await SignInAsync(SeedIds.AdminPhone)).Token);
        var block = await admin.PostAsJsonAsync($"/api/admin/users/{session.UserId}/block", new { reason = "Abusive messages" });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/bookings/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.Refresh)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(otherDevice.Refresh)).Status);
        Assert.Equal(RefreshTokenRevokeReason.Blocked, await RevokeReasonAsync(session.Refresh));
        Assert.Equal(RefreshTokenRevokeReason.Blocked, await RevokeReasonAsync(otherDevice.Refresh));

        var unblock = await admin.PostAsJsonAsync($"/api/admin/users/{session.UserId}/unblock", new { });
        Assert.Equal(HttpStatusCode.OK, unblock.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.Refresh)).Status);
        var back = await SignInAsync(session.Phone);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(back.Refresh)).Status);
    }

    [Fact]
    public async Task A_blocked_user_with_a_live_token_is_refused_and_revoked_on_refresh()
    {
        var session = await SignUpAsync("Refresh Blocked Directly");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsBlocked = true;
            await db.SaveChangesAsync();
        }

        var refused = await RefreshAsync(session.Refresh);
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Contains("suspended", refused.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RefreshTokenRevokeReason.Blocked, await RevokeReasonAsync(session.Refresh));
    }

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Session> SignUpAsync(string name)
    {
        var phone = "+9196" + Random.Shared.Next(10000000, 99999999).ToString();
        var client = Client();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender = "Female", isNewUser = true });
        return await VerifyAsync(client, phone, otp.DevCode);
    }

    private async Task<Session> SignInAsync(string phone)
    {
        var client = Client();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        return await VerifyAsync(client, phone, otp.DevCode);
    }

    private static async Task<Session> VerifyAsync(HttpClient client, string phone, string? code)
    {
        var response = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var auth = JsonSerializer.Deserialize<AuthBody>(payload, Json)!;
        var setCookie = SetCookieOf(response) ?? throw new InvalidOperationException("No refresh cookie.");
        return new Session(phone, auth.User.Id, auth.Token, CookieValue(setCookie)!, setCookie);
    }

    private async Task<RefreshResult> RefreshAsync(string? refresh)
    {
        var response = await SendWithCookieAsync("/api/auth/refresh", refresh);
        var payload = await response.Content.ReadAsStringAsync();
        var setCookie = SetCookieOf(response);
        var rotated = setCookie is null ? null : CookieValue(setCookie);
        return response.IsSuccessStatusCode
            ? new RefreshResult(response.StatusCode, JsonSerializer.Deserialize<AuthBody>(payload, Json), rotated, "")
            : new RefreshResult(response.StatusCode, null, rotated, JsonSerializer.Deserialize<ErrorBody>(payload, Json)?.Error ?? "");
    }

    private Task<HttpResponseMessage> SendWithCookieAsync(string path, string? refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refresh is not null)
            request.Headers.Add("Cookie", $"{CookieName}={refresh}");
        return Client().SendAsync(request);
    }

    private async Task BackdateRevocationAsync(string refresh, TimeSpan by)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var hash = RefreshTokenService.Hash(refresh);
        var token = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
        token.RevokedAt = token.RevokedAt!.Value - by;
        await db.SaveChangesAsync();
    }

    private async Task<RefreshTokenRevokeReason?> RevokeReasonAsync(string refresh)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var hash = RefreshTokenService.Hash(refresh);
        return await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => t.RevokeReason).SingleAsync();
    }

    private static string? SetCookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    private static string? CookieValue(string setCookie)
    {
        var value = setCookie.Split(';')[0][(CookieName.Length + 1)..];
        return value.Length == 0 ? null : value;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record Session(string Phone, Guid UserId, string Token, string Refresh, string SetCookie);
    private sealed record RefreshResult(HttpStatusCode Status, AuthBody? Body, string? Refresh, string Error);
    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserBody(Guid Id, string Phone, string Role);
    private sealed record AuthBody(string Token, UserBody User);
}

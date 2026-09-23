extern alias WebApp;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using WebApp::YogaMarketplace.Web.Copy;
using WebApp::YogaMarketplace.Web.Services;
using YogaMarketplace.Api.Tests;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Web.Tests;

public class AdminPageTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _api;

    public AdminPageTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Admin_otp_lands_on_the_admin_area()
    {
        await using var web = CreateWeb(_api);
        var (admin, landing) = await SignInExistingAsync(web, SeedIds.AdminPhone);
        Assert.Equal("/admin", landing);

        var dashboard = await admin.GetStringAsync("/admin");
        Assert.Contains(UiCopy.AdminDashboardTitle, dashboard);
        Assert.Contains("data-role=\"Admin\"", dashboard);
        Assert.Contains("href=\"/admin/approvals\"", dashboard);
        Assert.Contains("href=\"/admin/users\"", dashboard);
        Assert.Contains("href=\"/admin/masters\"", dashboard);
        Assert.DoesNotContain("href=\"/bookings\"", dashboard);
        Assert.DoesNotContain("href=\"/instructor/bookings\"", dashboard);

        var users = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/users?role=Admin"));
        Assert.Contains(SeedIds.AdminPhone, users);
        Assert.Contains("data-role=\"Admin\"", users);
        Assert.DoesNotContain("codeHash", users, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", users, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("devCode", users, StringComparison.OrdinalIgnoreCase);

        var userId = Regex.Match(users, "data-user-id=\"([0-9a-fA-F-]{36})\"");
        Assert.True(userId.Success, users);
        var detail = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/users/{userId.Groups[1].Value}"));
        Assert.Contains(SeedIds.AdminPhone, detail);
        Assert.Contains(UiCopy.NoProviderRecord, detail);
        Assert.DoesNotContain("codeHash", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otp", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("devCode", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_and_provider_cannot_open_admin_pages()
    {
        await using var web = CreateWeb(_api);
        var anon = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var blocked = await anon.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/account/sign-in", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var customer = await SignInNewAsync(web);
        await AssertDeniedAsync(customer, "/admin", "/bookings");
        var customerHome = await customer.GetStringAsync("/areas");
        Assert.DoesNotContain("href=\"/admin\"", customerHome);
        var customerBookings = await customer.GetStringAsync("/bookings");
        Assert.DoesNotContain("href=\"/admin\"", customerBookings);

        var deniedPost = await customer.PostAsync(
            $"/admin/approvals?handler=Verify&id={Guid.NewGuid()}",
            Form(customerBookings, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, deniedPost.StatusCode);
        Assert.Contains("/account/access-denied", deniedPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var (provider, providerLanding) = await SignInExistingAsync(web, SeedIds.AnanyaPhone);
        Assert.Equal("/instructor/bookings", providerLanding);
        await AssertDeniedAsync(provider, "/admin/approvals", "/instructor/bookings");
        var inbox = await provider.GetStringAsync("/instructor/bookings");
        Assert.DoesNotContain("href=\"/admin\"", inbox);

        var (admin, _) = await SignInExistingAsync(web, SeedIds.AdminPhone);
        await AssertDeniedAsync(admin, "/bookings", "/admin");
        await AssertDeniedAsync(admin, "/instructor/bookings", "/admin");
    }

    [Fact]
    public async Task Verify_pending_provider_lists_them_for_browse_and_does_not_book()
    {
        var name = "Meera" + Random.Shared.Next(100000, 999999);
        var created = await RegisterPendingAsync(name);
        await using var web = CreateWeb(_api);
        var (admin, _) = await SignInExistingAsync(web, SeedIds.AdminPhone);

        var bookingsBefore = await admin.GetStringAsync("/admin/bookings");
        var beforeIds = BookingIds(bookingsBefore);
        Assert.DoesNotContain("data-action=\"accept\"", bookingsBefore);
        Assert.Contains(UiCopy.AdminBookingsLead, bookingsBefore);

        var hidden = await admin.GetStringAsync("/instructors?area=Bandra");
        Assert.DoesNotContain(name, hidden);

        var queue = await admin.GetStringAsync("/admin/approvals");
        var card = Article(queue, name);
        Assert.Contains("data-status=\"Pending\"", card);
        Assert.Contains("data-action=\"verify\"", card);
        Assert.Contains("data-action=\"reject\"", card);
        Assert.Contains("meet.google.com", card, StringComparison.OrdinalIgnoreCase);
        var id = Attr(card, "data-provider-id");

        var verified = await admin.PostAsync(
            $"/admin/approvals?handler=Verify&id={id}&status={ProviderApprovalStatuses.Pending}",
            Form(queue, new Dictionary<string, string>()));
        var verifiedBody = await verified.Content.ReadAsStringAsync();
        Assert.True(verified.StatusCode == HttpStatusCode.Redirect, verifiedBody);
        Assert.Contains($"notice={AdminNotices.Verified}", verified.Headers.Location?.OriginalString);

        var notice = await admin.GetStringAsync(verified.Headers.Location);
        Assert.Contains(UiCopy.ProviderVerifiedNotice, notice);
        Assert.DoesNotContain(name, notice);

        var listed = await admin.GetStringAsync($"/admin/approvals?status={ProviderApprovalStatuses.Verified}");
        var verifiedCard = Article(listed, name);
        Assert.Contains("data-status=\"Verified\"", verifiedCard);
        Assert.DoesNotContain("data-action=\"verify\"", verifiedCard);

        var browse = await admin.GetStringAsync("/instructors?area=Bandra");
        Assert.Contains(name, browse);
        Assert.DoesNotContain("meet.google.com", browse, StringComparison.OrdinalIgnoreCase);

        var bookingsAfter = await admin.GetStringAsync("/admin/bookings");
        Assert.Equal(beforeIds, BookingIds(bookingsAfter));
        Assert.DoesNotContain(name, bookingsAfter);
        Assert.DoesNotContain("data-action=\"accept\"", bookingsAfter);
        Assert.Equal(created.Id, Guid.Parse(id));
    }

    [Fact]
    public async Task Reject_pending_provider_keeps_the_reason_and_hides_them()
    {
        var name = "Rohit" + Random.Shared.Next(100000, 999999);
        await RegisterPendingAsync(name);
        await using var web = CreateWeb(_api);
        var (admin, _) = await SignInExistingAsync(web, SeedIds.AdminPhone);

        var queue = await admin.GetStringAsync("/admin/approvals");
        var card = Article(queue, name);
        var id = Attr(card, "data-provider-id");
        Assert.Contains("data-status=\"Pending\"", card);

        var tooLong = await admin.PostAsync(
            $"/admin/approvals?handler=Reject&id={id}&status={ProviderApprovalStatuses.Pending}",
            Form(queue, new Dictionary<string, string>
            {
                ["Reason"] = new string('x', AdminInput.MaxRejectionReasonLength + 1)
            }));
        var tooLongBody = await tooLong.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, tooLong.StatusCode);
        Assert.Contains(UiCopy.RejectionReasonTooLong, tooLongBody);
        var stillPending = Article(tooLongBody, name);
        Assert.Contains("data-status=\"Pending\"", stillPending);
        Assert.Contains("data-action=\"verify\"", stillPending);

        var rejected = await admin.PostAsync(
            $"/admin/approvals?handler=Reject&id={id}&status={ProviderApprovalStatuses.Pending}",
            Form(tooLongBody, new Dictionary<string, string>
            {
                ["Reason"] = "  Incomplete profile  "
            }));
        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        Assert.True(rejected.StatusCode == HttpStatusCode.Redirect, rejectedBody);
        Assert.Contains($"notice={AdminNotices.Rejected}", rejected.Headers.Location?.OriginalString);

        var notice = await admin.GetStringAsync(rejected.Headers.Location);
        Assert.Contains(UiCopy.ProviderRejectedNotice, notice);

        var listed = await admin.GetStringAsync($"/admin/approvals?status={ProviderApprovalStatuses.Rejected}");
        var rejectedCard = Article(listed, name);
        Assert.Contains("data-status=\"Rejected\"", rejectedCard);
        Assert.Contains("data-reason=\"Incomplete profile\"", rejectedCard);
        Assert.Contains("Incomplete profile", rejectedCard);
        Assert.DoesNotContain("data-action=\"reject\"", rejectedCard);

        var browse = await admin.GetStringAsync("/instructors?area=Bandra");
        Assert.DoesNotContain(name, browse);
    }

    [Fact]
    public async Task Reports_page_loads_the_summary()
    {
        await using var web = CreateWeb(_api);
        var (admin, _) = await SignInExistingAsync(web, SeedIds.AdminPhone);
        var dashboard = await admin.GetStringAsync("/admin");
        Assert.Contains(UiCopy.AdminDashboardTitle, dashboard);
        Assert.Contains(UiCopy.GmvPaid, dashboard);
        Assert.Contains(UiCopy.PendingPayouts, dashboard);
        Assert.Contains("data-currency=\"INR\"", dashboard);
        Assert.Contains("data-payout-count=", dashboard);
        Assert.Contains("data-payout-gross=", dashboard);
        Assert.Contains("data-payout-net=", dashboard);
        foreach (var status in BookingStatuses.All)
            Assert.Contains($"data-status=\"{status}\"", dashboard);
    }

    [Fact]
    public async Task Admin_can_add_an_area_and_update_masters()
    {
        await using var web = CreateWeb(_api);
        var (admin, _) = await SignInExistingAsync(web, SeedIds.AdminPhone);
        var html = await admin.GetStringAsync("/admin/masters");
        Assert.Contains("Bandra", html);
        Assert.Contains("data-slug=\"yoga\"", html);
        Assert.Contains(UiCopy.PlatformFee, html);

        var areaName = "Colaba" + Random.Shared.Next(1000, 9999);
        var created = await admin.PostAsync(
            "/admin/masters?handler=CreateArea",
            Form(html, new Dictionary<string, string>
            {
                ["name"] = areaName,
                ["city"] = "Mumbai"
            }));
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Redirect, createdBody);
        var saved = await admin.GetStringAsync(created.Headers.Location);
        Assert.Contains(UiCopy.AreaSavedNotice, saved);
        Assert.Contains($"data-area-name=\"{areaName}\" data-active=\"true\"", saved);

        var fee = InputValue(saved, "platform-fee");
        var cancel = InputValue(saved, "cancel-hours");
        var reschedule = InputValue(saved, "reschedule-hours");
        var late = InputValue(saved, "late-fee");
        var originalNote = TextArea(saved, "policy-note");
        var categoryId = Attr(Regex.Match(saved, "<article\\b[^>]*data-slug=\"yoga\"[^>]*>").Value, "data-category-id");
        const string renamed = "Hatha Yoga";
        const string probeNote = "Admin UI policy note.";

        try
        {
            var policy = await admin.PostAsync(
                "/admin/masters?handler=UpdatePolicy",
                Form(saved, new Dictionary<string, string>
                {
                    ["platformFeePercent"] = fee,
                    ["cancelFreeWindowHours"] = cancel,
                    ["rescheduleFreeWindowHours"] = reschedule,
                    ["lateCancelFeePercent"] = late,
                    ["policyNote"] = probeNote
                }));
            var policyBody = await policy.Content.ReadAsStringAsync();
            Assert.True(policy.StatusCode == HttpStatusCode.Redirect, policyBody);
            var policyPage = await admin.GetStringAsync(policy.Headers.Location);
            Assert.Contains(UiCopy.PolicySavedNotice, policyPage);
            Assert.Contains(probeNote, policyPage);

            var category = await admin.PostAsync(
                $"/admin/masters?handler=RenameCategory&id={categoryId}",
                Form(policyPage, new Dictionary<string, string>
                {
                    ["name"] = renamed
                }));
            var categoryBody = await category.Content.ReadAsStringAsync();
            Assert.True(category.StatusCode == HttpStatusCode.Redirect, categoryBody);
            var categoryPage = await admin.GetStringAsync(category.Headers.Location);
            Assert.Contains(UiCopy.CategorySavedNotice, categoryPage);
            Assert.Contains("data-slug=\"yoga\"", categoryPage);
            Assert.Contains($"value=\"{renamed}\"", categoryPage);
        }
        finally
        {
            var current = await admin.GetStringAsync("/admin/masters");
            await admin.PostAsync(
                $"/admin/masters?handler=RenameCategory&id={categoryId}",
                Form(current, new Dictionary<string, string> { ["name"] = "Yoga" }));
            current = await admin.GetStringAsync("/admin/masters");
            await admin.PostAsync(
                "/admin/masters?handler=UpdatePolicy",
                Form(current, new Dictionary<string, string>
                {
                    ["platformFeePercent"] = fee,
                    ["cancelFreeWindowHours"] = cancel,
                    ["rescheduleFreeWindowHours"] = reschedule,
                    ["lateCancelFeePercent"] = late,
                    ["policyNote"] = originalNote
                }));
        }
    }

    private async Task<RegisteredProvider> RegisterPendingAsync(string name)
    {
        var client = _api.CreateClient();
        var phone = "+9198" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostJsonAsync<OtpBody>(client, "/api/auth/otp/request", new
        {
            phone,
            name,
            gender = "Female",
            isNewUser = true
        });
        var auth = await PostJsonAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var areas = await client.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
        var bandra = areas!.Single(area => area.Name == "Bandra");
        var created = await PostJsonAsync<RegisterBody>(client, "/api/providers/register", new
        {
            displayName = name,
            age = 29,
            areaId = bandra.Id,
            offersOnline = true,
            onlineRate = 700,
            googleMeetLink = "https://meet.google.com/admin-ui-review"
        });
        Assert.Equal("Pending", created.Provider.Status);
        return new RegisteredProvider(created.Provider.Id, name);
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private WebApplicationFactory<WebApp::Program> CreateWeb(YogaApiFactory api)
    {
        _ = api.Server;
        return new WebApplicationFactory<WebApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = api.Server.BaseAddress.ToString(),
                    ["Api:CategorySlug"] = "yoga",
                    ["Api:TimeoutSeconds"] = "30"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HttpClientFactoryOptions>(MarketplaceApiClient.HttpClientName, options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = api.Server.CreateHandler();
                    });
                });
            });
        });
    }

    private static async Task<HttpClient> SignInNewAsync(WebApplicationFactory<WebApp::Program> web)
    {
        var client = Client(web);
        var phone = "98" + Random.Shared.Next(10000000, 99999999);
        var html = await client.GetStringAsync("/account/sign-in");
        var requested = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "new",
            ["Name"] = "Rahul Sharma",
            ["Gender"] = "Male",
            ["Phone"] = phone
        }));
        html = await requested.Content.ReadAsStringAsync();
        Assert.True(requested.IsSuccessStatusCode, html);
        var code = Regex.Match(html, "data-dev-code=\"([^\"]+)\"");
        Assert.True(code.Success, html);

        var verified = await client.PostAsync("/account/sign-in?handler=Verify", Form(html, new Dictionary<string, string>
        {
            ["Phone"] = phone,
            ["Code"] = code.Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.Equal("/areas", verified.Headers.Location?.OriginalString);
        return client;
    }

    private static async Task<(HttpClient Client, string? Landing)> SignInExistingAsync(WebApplicationFactory<WebApp::Program> web, string phone)
    {
        var client = Client(web);
        var html = await client.GetStringAsync("/account/sign-in");
        var requested = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "existing",
            ["Phone"] = phone
        }));
        html = await requested.Content.ReadAsStringAsync();
        Assert.True(requested.IsSuccessStatusCode, html);
        var code = Regex.Match(html, "data-dev-code=\"([^\"]+)\"");
        Assert.True(code.Success, html);

        var verified = await client.PostAsync("/account/sign-in?handler=Verify", Form(html, new Dictionary<string, string>
        {
            ["Phone"] = phone,
            ["Code"] = code.Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        return (client, verified.Headers.Location?.OriginalString);
    }

    private static async Task AssertDeniedAsync(HttpClient client, string path, string expectedLink)
    {
        var denied = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/account/access-denied", denied.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        var page = await client.GetStringAsync(denied.Headers.Location);
        Assert.Contains(UiCopy.AccessDeniedTitle, page);
        Assert.Contains($"href=\"{expectedLink}\"", page);
    }

    private static string Article(string html, string name)
    {
        var start = Regex.Match(html, $"<article\\b[^>]*data-provider-name=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(start.Success, html);
        var rest = html[start.Index..];
        var end = rest.IndexOf("</article>", StringComparison.OrdinalIgnoreCase);
        Assert.True(end > 0, html);
        return rest[..(end + "</article>".Length)];
    }

    private static string Attr(string tag, string name)
    {
        var match = Regex.Match(tag, $"{name}=\"([^\"]*)\"");
        Assert.True(match.Success, tag);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string[] BookingIds(string html) =>
        Regex.Matches(html, "data-booking-id=\"([0-9a-fA-F-]{36})\"")
            .Select(match => match.Groups[1].Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static string InputValue(string html, string id)
    {
        var match = Regex.Match(html, $"id=\"{id}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, html);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string TextArea(string html, string id)
    {
        var match = Regex.Match(html, $"id=\"{id}\"[^>]*>([\\s\\S]*?)</textarea>");
        Assert.True(match.Success, html);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static HttpClient Client(WebApplicationFactory<WebApp::Program> web) =>
        web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static FormUrlEncodedContent Form(string html, IDictionary<string, string> fields)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in fields)
            pairs[pair.Key] = pair.Value;

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, html);
        pairs["__RequestVerificationToken"] = token.Groups[1].Value;
        return new FormUrlEncodedContent(pairs);
    }

    private sealed record RegisteredProvider(Guid Id, string Name);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record UserAuthBody(Guid Id, string? Name, string Phone, string? Gender, string Role);
    private sealed record AuthBody(string Token, UserAuthBody User);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record RegisterProviderBody(Guid Id, string Status);
    private sealed record RegisterBody(RegisterProviderBody Provider, string Token);
}

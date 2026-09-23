extern alias WebApp;

using System.Net;
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

public class BookingLifecyclePageTests : IClassFixture<YogaApiFactory>
{
    private const string DevKeySecret = "dev-only-not-a-live-key-secret";
    private const string InstructorPhone = "9876543210";

    private readonly YogaApiFactory _api;

    public BookingLifecyclePageTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Instructor_inbox_is_provider_only_and_states_are_explicit()
    {
        await using var web = CreateWeb(_api);
        var anon = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var blocked = await anon.GetAsync("/instructor/bookings");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/account/sign-in", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var customer = await SignInNewAsync(web);
        var denied = await customer.GetAsync("/instructor/bookings");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/account/access-denied", denied.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        var customerBookings = await customer.GetStringAsync("/bookings");
        Assert.DoesNotContain("/instructor/bookings", customerBookings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-action=\"review\"", customerBookings);

        var (instructor, landing) = await SignInExistingAsync(web, InstructorPhone);
        Assert.Equal("/instructor/bookings", landing);
        var mine = await instructor.GetAsync("/bookings");
        Assert.Equal(HttpStatusCode.Redirect, mine.StatusCode);
        Assert.Contains("/account/access-denied", mine.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var empty = await instructor.GetStringAsync("/instructor/bookings?status=noshow");
        Assert.Contains(UiCopy.InstructorInboxTitle, empty);
        Assert.Contains(UiCopy.NoInstructorBookings, empty);
        Assert.Contains("status=NoShow", empty);
        Assert.DoesNotContain(UiCopy.UnknownBookingStatus, empty);
        Assert.DoesNotContain("data-action=\"accept\"", empty);
        Assert.Contains(UiCopy.InstructorInboxNav, empty);

        var unknown = await instructor.GetStringAsync("/instructor/bookings?status=NotAStatus");
        Assert.Contains(UiCopy.UnknownBookingStatus, unknown);
        Assert.DoesNotContain(UiCopy.NoInstructorBookings, unknown);
        Assert.DoesNotContain("data-action=\"accept\"", unknown);
    }

    [Fact]
    public async Task Instructor_inbox_shows_an_error_when_the_api_is_down()
    {
        await using var web = CreateWeb(_api, services =>
        {
            var existing = services.Where(descriptor => descriptor.ServiceType == typeof(IInstructorBookingApi)).ToList();
            foreach (var descriptor in existing)
                services.Remove(descriptor);
            services.AddSingleton<IInstructorBookingApi>(new DownInstructorApi());
        });

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var inbox = WebUtility.HtmlDecode(await instructor.GetStringAsync("/instructor/bookings"));
        Assert.Contains(UiCopy.ApiUnreachable, inbox);
        Assert.DoesNotContain(UiCopy.NoInstructorBookings, inbox);
    }

    [Fact]
    public async Task Accept_then_complete_unlocks_one_review_and_rejects_a_second()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForStudioAsync(customer);

        var pendingCustomer = await customer.GetStringAsync("/bookings");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.PendingAccept), pendingCustomer);
        Assert.Contains(UiCopy.StatusPendingAccept, pendingCustomer);
        Assert.DoesNotContain("data-action=\"review\"", pendingCustomer);

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var pending = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.PendingAccept}");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.PendingAccept), pending);
        Assert.Contains("data-action=\"accept\"", pending);
        Assert.Contains("data-action=\"decline\"", pending);
        Assert.DoesNotContain("data-action=\"complete\"", pending);
        Assert.DoesNotContain("data-action=\"review\"", pending);

        var accepted = await instructor.PostAsync(
            $"/instructor/bookings?handler=Accept&id={bookingId}&status={BookingStatuses.PendingAccept}",
            Form(pending, new Dictionary<string, string>()));
        var acceptedBody = await accepted.Content.ReadAsStringAsync();
        Assert.True(accepted.StatusCode == HttpStatusCode.Redirect, acceptedBody);
        Assert.Contains($"notice={InstructorNotices.Accepted}", accepted.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var acceptedPage = await instructor.GetStringAsync(accepted.Headers.Location);
        Assert.Contains(UiCopy.AcceptedNotice, acceptedPage);

        var upcoming = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.Upcoming}");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), upcoming);
        Assert.Contains("data-action=\"complete\"", upcoming);
        Assert.DoesNotContain("data-action=\"accept\"", upcoming);

        var customerUpcoming = await customer.GetStringAsync("/bookings");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), customerUpcoming);
        Assert.Contains(UiCopy.StatusUpcoming, customerUpcoming);
        Assert.DoesNotContain(BookingStatuses.PendingAccept, customerUpcoming);
        Assert.DoesNotContain("data-action=\"review\"", customerUpcoming);

        var completed = await instructor.PostAsync(
            $"/instructor/bookings?handler=Complete&id={bookingId}&status={BookingStatuses.Upcoming}",
            Form(upcoming, new Dictionary<string, string>()));
        var completedBody = await completed.Content.ReadAsStringAsync();
        Assert.True(completed.StatusCode == HttpStatusCode.Redirect, completedBody);

        var doneInstructor = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.Completed}");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Completed), doneInstructor);
        Assert.Contains(UiCopy.CompletedNotice, await instructor.GetStringAsync(completed.Headers.Location));
        Assert.DoesNotContain("data-action=\"complete\"", doneInstructor);

        var ready = await customer.GetStringAsync("/bookings");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Completed), ready);
        Assert.Contains("data-action=\"review\"", ready);
        Assert.Contains("name=\"Comment\"", ready);
        Assert.Contains("name=\"Rating\"", ready);
        Assert.DoesNotContain(UiCopy.ReviewedAlready, ready);

        var reviewed = await customer.PostAsync(
            $"/bookings?handler=Review&id={bookingId}",
            Form(ready, new Dictionary<string, string>
            {
                ["Rating"] = "5",
                ["Comment"] = "  Calm and clear  "
            }));
        var reviewedBody = await reviewed.Content.ReadAsStringAsync();
        Assert.True(reviewed.StatusCode == HttpStatusCode.Redirect, reviewedBody);
        var location = reviewed.Headers.Location?.OriginalString ?? "";
        Assert.Contains("/bookings", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewed=", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ym.reviewed", SetCookie(reviewed), StringComparison.OrdinalIgnoreCase);

        var saved = await customer.GetStringAsync(reviewed.Headers.Location);
        Assert.Contains(UiCopy.ReviewedAlready, saved);
        Assert.DoesNotContain("data-action=\"review\"", saved);

        var again = await customer.PostAsync(
            $"/bookings?handler=Review&id={bookingId}",
            Form(saved, new Dictionary<string, string>
            {
                ["Rating"] = "4",
                ["Comment"] = "Second thought"
            }));
        var againBody = await again.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("already", againBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-action=\"review\"", againBody);
        Assert.Contains(UiCopy.ReviewedAlready, againBody);
        Assert.DoesNotContain("ym.reviewed", SetCookie(again), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decline_removes_pending_accept_and_frees_the_slot()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForStudioAsync(customer);

        var hidden = await customer.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        Assert.DoesNotContain(slotId.ToString(), hidden);

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var pending = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.PendingAccept}");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.PendingAccept), pending);

        var declined = await instructor.PostAsync(
            $"/instructor/bookings?handler=Decline&id={bookingId}&status={BookingStatuses.PendingAccept}",
            Form(pending, new Dictionary<string, string>()));
        var declinedBody = await declined.Content.ReadAsStringAsync();
        Assert.True(declined.StatusCode == HttpStatusCode.Redirect, declinedBody);

        var notice = await instructor.GetStringAsync(declined.Headers.Location);
        Assert.Contains(UiCopy.DeclinedNotice, notice);

        var listed = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.Declined}");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Declined), listed);
        Assert.Contains(PaymentStatuses.Refunded, listed);
        Assert.DoesNotContain("data-action=\"accept\"", listed);
        Assert.DoesNotContain("data-action=\"decline\"", listed);

        var mine = await customer.GetStringAsync("/bookings");
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Declined), mine);
        Assert.Contains(PaymentStatuses.Refunded, mine);
        Assert.Contains(UiCopy.StatusDeclined, mine);
        Assert.DoesNotContain(BookingStatuses.PendingAccept, mine);
        Assert.DoesNotContain("data-action=\"review\"", mine);

        var profile = await customer.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        Assert.Contains(slotId.ToString(), profile);
    }

    private WebApplicationFactory<WebApp::Program> CreateWeb(YogaApiFactory api, Action<IServiceCollection>? configure = null)
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
                    ["Api:TimeoutSeconds"] = "30",
                    ["Payments:UseFakeCheckout"] = "true",
                    ["Payments:KeySecret"] = DevKeySecret
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
                configure?.Invoke(services);
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

    private static async Task<(Guid SlotId, Guid BookingId)> PayForStudioAsync(HttpClient client)
    {
        var profile = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        var link = BookLinks(profile, SessionModes.Studio).First();
        var bookPage = await client.GetStringAsync(link.Path);
        var started = await client.PostAsync(link.Path, Form(bookPage, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        var pay = await client.GetAsync(started.Headers.Location);
        var payHtml = await pay.Content.ReadAsStringAsync();
        Assert.True(pay.IsSuccessStatusCode, payHtml);

        var paid = await client.PostAsync("/bookings/pay?handler=Pay", Form(payHtml, new Dictionary<string, string>()));
        var paidBody = await paid.Content.ReadAsStringAsync();
        Assert.True(paid.StatusCode == HttpStatusCode.Redirect, paidBody);
        var booked = Regex.Match(paid.Headers.Location?.OriginalString ?? "", "booked=([0-9a-fA-F-]{36})");
        Assert.True(booked.Success, paid.Headers.Location?.OriginalString);
        return (link.SlotId, Guid.Parse(booked.Groups[1].Value));
    }

    private static HttpClient Client(WebApplicationFactory<WebApp::Program> web) =>
        web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static string BookingArticle(Guid bookingId, Guid slotId, string status) =>
        $"data-booking-id=\"{bookingId}\" data-slot-id=\"{slotId}\" data-status=\"{status}\"";

    private static string SetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join(";", values) : "";

    private static IReadOnlyList<BookLink> BookLinks(string html, string mode)
    {
        var links = new List<BookLink>();
        foreach (Match match in Regex.Matches(html, "href=\"([^\"]*bookings/new[^\"]+)\""))
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            var queryIndex = href.IndexOf('?');
            if (queryIndex < 0)
                continue;
            var parts = href[(queryIndex + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .Where(pair => pair.Length == 2)
                .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]), StringComparer.OrdinalIgnoreCase);
            if (!parts.TryGetValue("mode", out var linkMode) || !linkMode.Equals(mode, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!parts.TryGetValue("slotId", out var slotText) || !Guid.TryParse(slotText, out var slotId))
                continue;
            links.Add(new BookLink(href, slotId));
        }

        Assert.NotEmpty(links);
        return links;
    }

    private static FormUrlEncodedContent Form(string html, IDictionary<string, string> fields)
    {
        var pairs = HiddenFields(html);
        foreach (var pair in fields)
            pairs[pair.Key] = pair.Value;

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, html);
        pairs["__RequestVerificationToken"] = token.Groups[1].Value;
        return new FormUrlEncodedContent(pairs);
    }

    private static Dictionary<string, string> HiddenFields(string html)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = input.Value;
            if (!Regex.IsMatch(tag, "type=\"hidden\"", RegexOptions.IgnoreCase))
                continue;
            var name = Regex.Match(tag, "name=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
            if (name.Length == 0 || name.StartsWith("__", StringComparison.Ordinal))
                continue;
            var value = Regex.Match(tag, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
            pairs[name] = WebUtility.HtmlDecode(value);
        }

        return pairs;
    }

    private sealed class DownInstructorApi : IInstructorBookingApi
    {
        public Task<ApiResult<List<BookingDto>>> ListAsync(string? status, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<List<BookingDto>>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<BookingDto>> AcceptAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<BookingDto>.Fail(UiCopy.GenericError));

        public Task<ApiResult<BookingDto>> DeclineAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<BookingDto>.Fail(UiCopy.GenericError));

        public Task<ApiResult<BookingDto>> CompleteAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<BookingDto>.Fail(UiCopy.GenericError));
    }

    private sealed record BookLink(string Path, Guid SlotId);
}

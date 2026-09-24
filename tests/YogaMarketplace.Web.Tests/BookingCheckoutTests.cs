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

public class BookingCheckoutTests : IClassFixture<YogaApiFactory>
{
    private const string DevKeySecret = "dev-only-not-a-live-key-secret";

    private readonly YogaApiFactory _api;

    public BookingCheckoutTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Bookings_require_sign_in()
    {
        await using var web = CreateWeb(_api);
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var bookings = await client.GetAsync("/bookings");
        Assert.Equal(HttpStatusCode.Redirect, bookings.StatusCode);
        Assert.Contains("/account/sign-in", bookings.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var pay = await client.GetAsync("/bookings/new");
        Assert.Equal(HttpStatusCode.Redirect, pay.StatusCode);

        var slots = await client.GetAsync("/bookings?handler=Slots&id=" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Redirect, slots.StatusCode);
        Assert.Contains("/account/sign-in", slots.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Home_booking_collects_address_then_pay_confirms_pending_accept()
    {
        await using var web = CreateWeb(_api);
        var client = await SignInAsync(web);
        var providerId = SeedIds.AnanyaProviderId;
        var profile = await client.GetStringAsync($"/instructors/{providerId}?mode={SessionModes.Home}");
        var link = BookLinks(profile, SessionModes.Home).First();
        Assert.Contains("bookings/new", profile);

        var bookPage = await client.GetStringAsync(link.Path);
        Assert.Contains(UiCopy.HomeAddressLead, bookPage);
        Assert.Contains("name=\"HomeAddress\"", bookPage);
        Assert.Contains("name=\"Landmark\"", bookPage);
        Assert.Contains("Bandra, Mumbai", bookPage);
        Assert.DoesNotContain("Lotus Studio", bookPage);

        var missing = await client.PostAsync(link.Path, Form(bookPage, new Dictionary<string, string>
        {
            ["HomeAddress"] = "   ",
            ["Landmark"] = ""
        }));
        var missingBody = await missing.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Contains(UiCopy.AddressRequired, missingBody);
        Assert.DoesNotContain("order_fake_", missingBody);
        Assert.DoesNotContain("Home sessions require an address and a landmark.", missingBody);

        var addressOnly = await client.PostAsync(link.Path, Form(missingBody, new Dictionary<string, string>
        {
            ["HomeAddress"] = "14th Road, Bandra West",
            ["Landmark"] = "  "
        }));
        var addressBody = await addressOnly.Content.ReadAsStringAsync();
        Assert.Contains(UiCopy.LandmarkRequired, addressBody);
        Assert.DoesNotContain("order_fake_", addressBody);

        var landmarkOnly = await client.PostAsync(link.Path, Form(addressBody, new Dictionary<string, string>
        {
            ["HomeAddress"] = "",
            ["Landmark"] = "Near the station"
        }));
        var landmarkBody = await landmarkOnly.Content.ReadAsStringAsync();
        Assert.Contains(UiCopy.AddressRequired, landmarkBody);

        var started = await client.PostAsync(link.Path, Form(landmarkBody, new Dictionary<string, string>
        {
            ["HomeAddress"] = "  14th Road, Bandra West  ",
            ["Landmark"] = " Near the station "
        }));
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.Equal("/bookings/pay", started.Headers.Location?.OriginalString);

        var pay = await client.GetAsync(started.Headers.Location);
        var payHtml = await pay.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        Assert.Contains("order_fake_", payHtml);
        Assert.Contains(UiCopy.PayNow, payHtml);
        Assert.Contains("899", payHtml);
        Assert.Contains("14th Road, Bandra West", payHtml);
        Assert.Contains("Bandra, Mumbai", payHtml);
        Assert.DoesNotContain("Lotus Studio", payHtml);
        Assert.DoesNotContain(DevKeySecret, payHtml);
        Assert.DoesNotContain("checkout.razorpay.com", payHtml);
        Assert.DoesNotContain("/api/webhooks/razorpay", payHtml);

        var before = await client.GetStringAsync("/bookings");
        Assert.Contains(UiCopy.NoBookings, before);
        Assert.DoesNotContain(BookingStatuses.PendingAccept, before);

        var paid = await client.PostAsync("/bookings/pay?handler=Pay", Form(payHtml, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        Assert.StartsWith("/bookings?", paid.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("booked=", paid.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var mine = await client.GetStringAsync(paid.Headers.Location);
        Assert.Contains(UiCopy.PaymentReceived, mine);
        Assert.Contains(BookingStatuses.PendingAccept, mine);
        Assert.Contains(PaymentStatuses.Paid, mine);
        Assert.Contains("Ananya Desai", mine);
        Assert.Contains("14th Road, Bandra West", mine);
        Assert.Contains("Near the station", mine);
        Assert.Contains(SessionModes.Home, mine);
        Assert.DoesNotContain("meet.google.com", mine, StringComparison.OrdinalIgnoreCase);

        var after = await client.GetStringAsync($"/instructors/{providerId}?mode={SessionModes.Home}");
        Assert.DoesNotContain(link.SlotId.ToString(), after);
    }

    [Fact]
    public async Task Abandoned_checkout_leaves_no_booking()
    {
        await using var web = CreateWeb(_api);
        var client = await SignInAsync(web);
        var (path, slotId, payHtml) = await OpenStudioPayAsync(client);

        Assert.Contains("order_fake_", payHtml);
        Assert.Contains(UiCopy.AbandonPayment, payHtml);
        Assert.DoesNotContain("name=\"HomeAddress\"", await client.GetStringAsync(path));

        var abandoned = await client.PostAsync("/bookings/pay?handler=Abandon", Form(payHtml, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, abandoned.StatusCode);
        Assert.Contains("notice=abandoned", abandoned.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var mine = await client.GetStringAsync(abandoned.Headers.Location);
        Assert.Contains(UiCopy.PaymentAbandoned, mine);
        Assert.Contains(UiCopy.NoBookings, mine);
        Assert.DoesNotContain(BookingStatuses.PendingAccept, mine);
        Assert.DoesNotContain(slotId.ToString(), mine);

        var profile = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        Assert.Contains(slotId.ToString(), profile);
    }

    [Fact]
    public async Task Failed_checkout_leaves_no_booking()
    {
        await using var web = CreateWeb(_api);
        var client = await SignInAsync(web);
        var (_, slotId, payHtml) = await OpenStudioPayAsync(client);

        var failed = await client.PostAsync("/bookings/pay?handler=Fail", Form(payHtml, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        Assert.Contains("notice=failed", failed.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var mine = await client.GetStringAsync(failed.Headers.Location);
        Assert.Contains(UiCopy.PaymentFailed, mine);
        Assert.Contains(UiCopy.NoBookings, mine);
        Assert.DoesNotContain(BookingStatuses.PendingAccept, mine);

        var profile = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        Assert.Contains(slotId.ToString(), profile);
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
            });
        });
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<WebApp::Program> web)
    {
        var client = web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
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
        return client;
    }

    private static async Task<(string Path, Guid SlotId, string PayHtml)> OpenStudioPayAsync(HttpClient client)
    {
        var profile = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode={SessionModes.Studio}");
        var link = BookLinks(profile, SessionModes.Studio).First();
        var bookPage = await client.GetStringAsync(link.Path);
        Assert.DoesNotContain("name=\"HomeAddress\"", bookPage);
        Assert.Contains("749", bookPage);
        Assert.Contains("Bandra, Mumbai", bookPage);
        Assert.Contains("Lotus Studio", bookPage);

        var started = await client.PostAsync(link.Path, Form(bookPage, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        var pay = await client.GetAsync(started.Headers.Location);
        var payHtml = await pay.Content.ReadAsStringAsync();
        Assert.True(pay.IsSuccessStatusCode, payHtml);
        Assert.Contains("Bandra, Mumbai", payHtml);
        Assert.Contains("Lotus Studio", payHtml);
        Assert.Contains("749", payHtml);
        return (link.Path, link.SlotId, payHtml);
    }

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

    private sealed record BookLink(string Path, Guid SlotId);
}

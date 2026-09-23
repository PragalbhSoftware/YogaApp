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

public class InstructorBrowsePageTests : IClassFixture<YogaApiFactory>
{
    private readonly YogaApiFactory _api;

    public InstructorBrowsePageTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Home_is_indexable_and_private_pages_are_not()
    {
        await using var web = new WebApplicationFactory<WebApp::Program>();
        var client = web.CreateClient();

        var home = await client.GetStringAsync("/");
        Assert.Contains("<title>Find verified yoga instructors", home);
        Assert.Contains("home, studio, or online", home);
        Assert.Contains($"content=\"{UiCopy.HomeDescription}\"", home);
        Assert.DoesNotContain(UiCopy.CityName, home);
        Assert.Contains("property=\"og:title\"", home);
        Assert.Contains("property=\"og:description\"", home);
        Assert.Contains($"property=\"og:locale\" content=\"{UiCopy.OgLocale}\"", home);
        Assert.DoesNotContain("noindex", home);

        var index = await client.GetAsync("/Index");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.DoesNotContain("noindex", await index.Content.ReadAsStringAsync());

        var signIn = await client.GetStringAsync("/account/sign-in");
        Assert.Contains($"name=\"robots\" content=\"{PageSeo.RobotsNoIndex}\"", signIn);
        Assert.DoesNotContain("property=\"og:title\"", signIn);

        var denied = await client.GetStringAsync("/account/access-denied");
        Assert.Contains($"name=\"robots\" content=\"{PageSeo.RobotsNoIndex}\"", denied);

        var error = await client.GetStringAsync("/Error");
        Assert.Contains($"name=\"robots\" content=\"{PageSeo.RobotsNoIndex}\"", error);
    }

    [Fact]
    public async Task Browse_keeps_a_get_form_and_the_list_handler_returns_one_fragment()
    {
        await using var web = CreateWeb(_api);
        var anonymous = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var blocked = await anonymous.GetAsync("/instructors?area=Bandra&handler=List");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/account/sign-in", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var client = await SignInAsync(web);

        var page = await client.GetAsync("/instructors?area=Bandra&mode=Home");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.False(page.Headers.Contains("X-Robots-Tag"));
        Assert.Contains("<form method=\"get\"", html);
        Assert.Contains("data-browse-filters", html);
        Assert.Contains("name=\"area\"", html);
        Assert.Contains("name=\"mode\"", html);
        Assert.Contains("value=\"Bandra\"", html);
        Assert.Contains("value=\"Home\"", html);
        Assert.Contains("browse.js", html);
        Assert.Contains(UiCopy.ApplyFilters, html);
        Assert.Contains("Ananya Desai", html);
        Assert.Contains("899", html);
        Assert.Contains(UiCopy.BookCta, html);
        Assert.Contains($"data-heading=\"{UiCopy.BrowseHeading}\"", html);
        Assert.Contains("<title>Home yoga instructors in Bandra", html);
        Assert.Contains("Yoga Marketplace</title>", html);
        Assert.DoesNotContain(UiCopy.CityName, TitleAndDescription(html));
        Assert.Contains(string.Format(UiCopy.BrowseDescriptionWithMode, "Home", "Bandra"), html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("property=\"og:url\"", html);
        Assert.Contains("area=Bandra", html);
        Assert.DoesNotContain("noindex", html);
        Assert.DoesNotContain("meet.google.com", html, StringComparison.OrdinalIgnoreCase);

        var bandra = await client.GetAsync("/instructors?area=Bandra&mode=Home&handler=List");
        var bandraHtml = await bandra.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, bandra.StatusCode);
        Assert.Contains("noindex", bandra.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("Ananya Desai", bandraHtml);
        Assert.Contains("899", bandraHtml);
        Assert.Contains(UiCopy.BookCta, bandraHtml);
        Assert.Contains("data-results-summary", bandraHtml);
        Assert.DoesNotContain("<html", bandraHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", bandraHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-browse-filters", bandraHtml);
        Assert.DoesNotContain("<title", bandraHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("meet.google.com", bandraHtml, StringComparison.OrdinalIgnoreCase);

        var andheri = await client.GetAsync("/instructors?area=Andheri&mode=Home&handler=List");
        var andheriHtml = await andheri.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, andheri.StatusCode);
        Assert.Contains("No verified instructors in Andheri", andheriHtml);
        Assert.Contains(UiCopy.EmptyBrowseHint, andheriHtml);
        Assert.DoesNotContain("Ananya Desai", andheriHtml);
        Assert.DoesNotContain("<form", andheriHtml, StringComparison.OrdinalIgnoreCase);

        var fallback = await client.GetAsync("/instructors?area=Andheri&mode=Home");
        var fallbackHtml = await fallback.Content.ReadAsStringAsync();
        Assert.Contains("<form method=\"get\"", fallbackHtml);
        Assert.Contains("No verified instructors in Andheri", fallbackHtml);
        Assert.Contains(UiCopy.ApplyFilters, fallbackHtml);
        Assert.Contains("<title>Home yoga instructors in Andheri", fallbackHtml);
        Assert.DoesNotContain("Ananya Desai", fallbackHtml);

        var remembered = await client.GetStringAsync("/instructors");
        Assert.Contains(string.Format(UiCopy.BrowseHeading, "Andheri"), remembered);

        var profile = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode=Home");
        var profileText = WebUtility.HtmlDecode(profile);
        Assert.Contains("<title>Ananya Desai, yoga in Bandra", profile);
        Assert.DoesNotContain(UiCopy.CityName, TitleAndDescription(profile));
        Assert.Contains(string.Format(UiCopy.ProfileDescription, "Ananya Desai", "Bandra"), profile);
        Assert.Contains("property=\"og:description\"", profile);
        Assert.DoesNotContain("noindex", profile);
        Assert.Contains(UiCopy.TrustVerified, profile);
        Assert.Contains(UiCopy.ReviewsFromCompleted, profile);
        Assert.Contains("aria-current=\"page\"", profile);
        Assert.Contains(UiCopy.SlotsLead, profile);
        Assert.Contains(string.Format(UiCopy.ShowingMode, SessionModes.Home, "₹899"), profileText);
        Assert.Contains("Lotus Studio", profile);
        Assert.Contains("07:00", profile);
        Assert.Contains("bookings/new", profile);
        Assert.DoesNotContain("meet.google.com", profile, StringComparison.OrdinalIgnoreCase);

        var online = await client.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode=Online");
        Assert.Contains("18:00", online);
        Assert.DoesNotContain("07:00", online);
        Assert.Contains("<title>Ananya Desai, yoga in Bandra", online);
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

    private static string TitleAndDescription(string html)
    {
        var title = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Value;
        var description = Regex.Match(html, "name=\"description\" content=\"(.*?)\"", RegexOptions.Singleline).Value;
        var ogTitle = Regex.Match(html, "property=\"og:title\" content=\"(.*?)\"", RegexOptions.Singleline).Value;
        var ogDescription = Regex.Match(html, "property=\"og:description\" content=\"(.*?)\"", RegexOptions.Singleline).Value;
        return title + description + ogTitle + ogDescription;
    }

    private static FormUrlEncodedContent Form(string html, IDictionary<string, string> fields)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, html);
        var pairs = fields.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)).ToList();
        pairs.Add(new KeyValuePair<string, string>("__RequestVerificationToken", token.Groups[1].Value));
        return new FormUrlEncodedContent(pairs);
    }
}

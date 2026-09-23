extern alias WebApp;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using WebApp::YogaMarketplace.Web.Copy;
using WebApp::YogaMarketplace.Web.Services;
using YogaMarketplace.Api.Tests;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Web.Tests;

public class InstructorAvailabilityPageTests : IClassFixture<YogaApiFactory>
{
    private const string DevKeySecret = "dev-only-not-a-live-key-secret";
    private const string InstructorPhone = "9876543210";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly YogaApiFactory _api;

    public InstructorAvailabilityPageTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Availability_is_provider_only_and_lists_the_week_by_mode()
    {
        await using var web = CreateWeb(_api);
        var anon = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var blocked = await anon.GetAsync("/instructor/availability");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/account/sign-in", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var customer = await SignInNewAsync(web);
        var denied = await customer.GetAsync("/instructor/availability");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/account/access-denied", denied.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        var customerBookings = await customer.GetStringAsync("/bookings");
        Assert.DoesNotContain("/instructor/availability", customerBookings, StringComparison.OrdinalIgnoreCase);

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var inbox = await instructor.GetStringAsync("/instructor/bookings");
        Assert.Contains("href=\"/instructor/availability\"", inbox);
        Assert.Contains(UiCopy.InstructorAvailabilityNav, inbox);

        var home = await instructor.GetStringAsync("/instructor/availability");
        Assert.Contains(UiCopy.InstructorAvailabilityTitle, home);
        Assert.Contains(UiCopy.InstructorAvailabilityLead, home);
        Assert.Contains("href=\"/instructor/bookings\"", home);
        var today = MumbaiClock.Today();
        Assert.Contains($"data-window-from=\"{today:yyyy-MM-dd}\"", home);
        Assert.Contains($"data-window-to=\"{today.AddDays(6):yyyy-MM-dd}\"", home);
        Assert.Contains("pill active", home);
        Assert.Contains("mode=Home", home);
        var homeSlots = ParseSlots(home);
        Assert.Contains(homeSlots, slot => slot.State == "open" && slot.Blocked == "false");
        Assert.Contains("07:00", home);
        Assert.Contains("08:00", home);
        Assert.DoesNotContain("data-action=\"delete\"", home);

        var studio = await instructor.GetStringAsync("/instructor/availability?mode=studio");
        Assert.Contains("data-mode=\"Studio\"", studio);
        Assert.DoesNotContain("data-mode=\"Home\"", studio);
        Assert.Contains("10:00", studio);
        Assert.DoesNotContain("07:00", studio);

        var unknown = await instructor.GetStringAsync("/instructor/availability?mode=NotAMode");
        Assert.Contains(UiCopy.ModeRequired, unknown);
        Assert.DoesNotContain("data-slot-id=", unknown);
        Assert.DoesNotContain(UiCopy.NoOwnedSlots, unknown);

        var backwards = await instructor.GetStringAsync(
            $"/instructor/availability?mode=Home&from={today.AddDays(6):yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Contains(UiCopy.DateOrder, backwards);
        Assert.DoesNotContain("data-slot-id=", backwards);
    }

    [Fact]
    public async Task Block_keeps_the_slot_on_the_list_and_hides_it_from_customers()
    {
        await using var web = CreateWeb(_api);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var page = await instructor.GetStringAsync("/instructor/availability?mode=Home");
        var open = ParseSlots(page).First(slot => slot.State == "open");
        var before = Article(page, open.Id);
        Assert.Contains("data-action=\"block\"", before);
        Assert.Contains(UiCopy.SlotOpen, before);
        Assert.Contains(UiCopy.BlockSlot, before);

        var blocked = await instructor.PostAsync(
            $"/instructor/availability?handler=Block&id={open.Id}&mode=Home",
            Form(page, new Dictionary<string, string>()));
        var blockedBody = await blocked.Content.ReadAsStringAsync();
        Assert.True(blocked.StatusCode == HttpStatusCode.Redirect, blockedBody);
        Assert.Contains("notice=blocked", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mode=Home", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var listed = await instructor.GetStringAsync(blocked.Headers.Location);
        Assert.Contains(UiCopy.BlockedNotice, listed);
        var card = Article(listed, open.Id);
        Assert.Contains("data-blocked=\"true\"", card);
        Assert.Contains("data-state=\"blocked\"", card);
        Assert.Contains(UiCopy.SlotBlocked, card);
        Assert.Contains(UiCopy.SlotBlockedDetail, card);
        Assert.DoesNotContain("data-action=\"block\"", card);
        Assert.Contains(ParseSlots(listed), slot => slot.State == "open");
        Assert.Contains("data-action=\"block\"", listed);

        var again = await instructor.PostAsync(
            $"/instructor/availability?handler=Block&id={open.Id}&mode=Home",
            Form(listed, new Dictionary<string, string>()));
        var againBody = await again.Content.ReadAsStringAsync();
        Assert.True(again.StatusCode == HttpStatusCode.Redirect, againBody);
        var still = Article(await instructor.GetStringAsync(again.Headers.Location), open.Id);
        Assert.Contains("data-state=\"blocked\"", still);
        Assert.Contains(UiCopy.BlockedNotice, await instructor.GetStringAsync(again.Headers.Location));

        var profile = await instructor.GetStringAsync($"/instructors/{SeedIds.AnanyaProviderId}?mode=Home");
        Assert.DoesNotContain(open.Id.ToString(), profile);
        Assert.Contains(open.Id.ToString(), listed);
    }

    [Fact]
    public async Task Occupying_booking_is_shown_and_block_surfaces_the_conflict()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var slotId = await FirstFutureSlotIdAsync(SessionModes.Studio);
        var bookingId = await PayForSlotAsync(customer, slotId, SessionModes.Studio);

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var page = await instructor.GetStringAsync("/instructor/availability?mode=Studio");
        var card = Article(page, slotId);
        Assert.Contains("data-state=\"occupied\"", card);
        Assert.Contains("data-blocked=\"false\"", card);
        Assert.Contains(UiCopy.SlotOccupied, card);
        Assert.Contains(UiCopy.SlotOccupiedDetail, card);
        Assert.DoesNotContain("data-action=\"block\"", card);
        Assert.Contains("data-action=\"block\"", page);

        var conflict = await instructor.PostAsync(
            $"/instructor/availability?handler=Block&id={slotId}&mode=Studio",
            Form(page, new Dictionary<string, string>()));
        var body = WebUtility.HtmlDecode(await conflict.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Contains("That slot has a booking.", body);
        var after = Article(body, slotId);
        Assert.Contains("data-state=\"occupied\"", after);
        Assert.Contains("data-blocked=\"false\"", after);
        Assert.DoesNotContain("data-action=\"block\"", after);
        Assert.Contains(bookingId.ToString(), await instructor.GetStringAsync("/instructor/bookings?status=PendingAccept"));
    }

    [Fact]
    public async Task Past_and_missing_slots_surface_the_api_error()
    {
        await using var web = CreateWeb(_api);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        var page = await instructor.GetStringAsync("/instructor/availability?mode=Home");
        var open = ParseSlots(page).First(slot => slot.State == "open");
        var yesterday = MumbaiClock.Today().AddDays(-1);
        await ShiftSlotToDateAsync(open.Id, yesterday);

        var day = $"{yesterday:yyyy-MM-dd}";
        var pastPage = await instructor.GetStringAsync($"/instructor/availability?mode=Home&from={day}&to={day}");
        Assert.Contains($"data-window-from=\"{day}\"", pastPage);
        var pastCard = Article(pastPage, open.Id);
        Assert.Contains("data-state=\"past\"", pastCard);
        Assert.Contains("data-blocked=\"false\"", pastCard);
        Assert.Contains(UiCopy.SlotPast, pastCard);
        Assert.DoesNotContain("data-action=\"block\"", pastCard);

        var past = await instructor.PostAsync(
            $"/instructor/availability?handler=Block&id={open.Id}&mode=Home&from={day}&to={day}",
            Form(page, new Dictionary<string, string>()));
        var pastBody = WebUtility.HtmlDecode(await past.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, past.StatusCode);
        Assert.Contains("Past slots cannot be blocked.", pastBody);
        var stillPast = Article(pastBody, open.Id);
        Assert.Contains("data-state=\"past\"", stillPast);
        Assert.Contains("data-blocked=\"false\"", stillPast);

        var missing = await instructor.PostAsync(
            $"/instructor/availability?handler=Block&id={Guid.NewGuid()}&mode=Home",
            Form(page, new Dictionary<string, string>()));
        var missingBody = WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Contains("Slot not found.", missingBody);
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

    private async Task<Guid> FirstFutureSlotIdAsync(string mode)
    {
        var list = await _api.CreateClient().GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode={Uri.EscapeDataString(mode)}",
            Json);
        Assert.NotNull(list);
        var slot = list!.Slots
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Start, StringComparer.Ordinal)
            .First();
        Assert.True(MumbaiClock.SessionStart(slot.Date, TimeOnly.ParseExact(slot.Start, "HH:mm", CultureInfo.InvariantCulture)) > DateTimeOffset.UtcNow);
        return slot.Id;
    }

    private static async Task<Guid> PayForSlotAsync(HttpClient client, Guid slotId, string mode)
    {
        var path = $"/bookings/new?providerId={SeedIds.AnanyaProviderId}&slotId={slotId}&mode={mode}";
        var bookPage = await client.GetStringAsync(path);
        Assert.Contains(slotId.ToString(), bookPage);
        var started = await client.PostAsync(path, Form(bookPage, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        var pay = await client.GetAsync(started.Headers.Location);
        var payHtml = await pay.Content.ReadAsStringAsync();
        Assert.True(pay.IsSuccessStatusCode, payHtml);

        var paid = await client.PostAsync("/bookings/pay?handler=Pay", Form(payHtml, new Dictionary<string, string>()));
        var paidBody = await paid.Content.ReadAsStringAsync();
        Assert.True(paid.StatusCode == HttpStatusCode.Redirect, paidBody);
        var booked = Regex.Match(paid.Headers.Location?.OriginalString ?? "", "booked=([0-9a-fA-F-]{36})");
        Assert.True(booked.Success, paid.Headers.Location?.OriginalString);
        return Guid.Parse(booked.Groups[1].Value);
    }

    private async Task ShiftSlotToDateAsync(Guid slotId, DateOnly date)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = await db.AvailabilitySlots.SingleAsync(item => item.Id == slotId);
        slot.Date = date;
        await db.SaveChangesAsync();
    }

    private static List<SlotRow> ParseSlots(string html) =>
        Regex.Matches(
                html,
                "data-slot-id=\"([0-9a-fA-F-]{36})\"\\s+data-blocked=\"(true|false)\"\\s+data-state=\"(open|blocked|occupied|past)\"")
            .Select(match => new SlotRow(Guid.Parse(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value))
            .ToList();

    private static string Article(string html, Guid slotId)
    {
        var marker = $"data-slot-id=\"{slotId}\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, html);
        var open = html.LastIndexOf("<article", start, StringComparison.OrdinalIgnoreCase);
        var close = html.IndexOf("</article>", start, StringComparison.OrdinalIgnoreCase);
        Assert.True(open >= 0 && close > open, html);
        return html[open..(close + "</article>".Length)];
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

    private sealed record SlotRow(Guid Id, string Blocked, string State);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
}

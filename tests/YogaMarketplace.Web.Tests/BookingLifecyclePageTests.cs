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

public class BookingLifecyclePageTests : IClassFixture<YogaApiFactory>
{
    private const string DevKeySecret = "dev-only-not-a-live-key-secret";
    private const string InstructorPhone = "9876543210";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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

    [Fact]
    public async Task Cancel_pending_accept_refunds_and_frees_the_slot()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(customer, SessionModes.Online);

        var pending = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var card = Article(pending, bookingId);
        Assert.Contains("data-action=\"cancel\"", card);
        Assert.Contains(UiCopy.CancelHint, card);
        Assert.DoesNotContain("data-action=\"reschedule\"", card);
        Assert.DoesNotContain(slotId.ToString(), await customer.GetStringAsync(Profile(SessionModes.Online)));

        var cancelled = await PostChangeAsync(customer, pending, "Cancel", bookingId);
        var body = await cancelled.Content.ReadAsStringAsync();
        Assert.True(cancelled.StatusCode == HttpStatusCode.Redirect, body);
        Assert.Contains($"notice={CustomerNotices.Cancelled}", cancelled.Headers.Location?.OriginalString);

        var page = WebUtility.HtmlDecode(await customer.GetStringAsync(cancelled.Headers.Location));
        Assert.Contains(UiCopy.CancelledNotice, page);
        var done = Article(page, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Cancelled), done);
        Assert.Contains(PaymentStatuses.Refunded, done);
        Assert.Contains(UiCopy.StatusCancelled, done);
        Assert.DoesNotContain("data-action=\"cancel\"", done);
        Assert.DoesNotContain("data-action=\"reschedule\"", done);
        Assert.Contains(slotId.ToString(), await customer.GetStringAsync(Profile(SessionModes.Online)));
    }

    [Fact]
    public async Task Cancel_upcoming_refunds_and_frees_the_slot()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(customer, SessionModes.Online);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        await AcceptAsync(instructor, bookingId);

        var upcoming = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var card = Article(upcoming, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), card);
        Assert.Contains("data-action=\"cancel\"", card);
        Assert.Contains("data-action=\"reschedule\"", card);

        var cancelled = await PostChangeAsync(customer, upcoming, "Cancel", bookingId);
        var body = await cancelled.Content.ReadAsStringAsync();
        Assert.True(cancelled.StatusCode == HttpStatusCode.Redirect, body);
        Assert.Contains($"notice={CustomerNotices.Cancelled}", cancelled.Headers.Location?.OriginalString);

        var page = WebUtility.HtmlDecode(await customer.GetStringAsync(cancelled.Headers.Location));
        Assert.Contains(UiCopy.CancelledNotice, page);
        var done = Article(page, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Cancelled), done);
        Assert.Contains(PaymentStatuses.Refunded, done);
        Assert.DoesNotContain("data-action=\"cancel\"", done);
        Assert.DoesNotContain("data-action=\"reschedule\"", done);
        Assert.Contains(slotId.ToString(), await customer.GetStringAsync(Profile(SessionModes.Online)));
    }

    [Fact]
    public async Task Reschedule_upcoming_moves_to_an_open_slot_and_keeps_payment_paid()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(customer, SessionModes.Online);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        await AcceptAsync(instructor, bookingId);

        var online = await SlotIdsAsync(SessionModes.Online);
        var otherModes = await SlotIdsAsync(SessionModes.Home);
        otherModes.UnionWith(await SlotIdsAsync(SessionModes.Studio));

        var upcoming = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var card = Article(upcoming, bookingId);
        var choices = OptionIds(card);
        Assert.NotEmpty(choices);
        Assert.DoesNotContain(slotId, choices);
        Assert.All(choices, id => Assert.Contains(id, online));
        Assert.All(choices, id => Assert.DoesNotContain(id, otherModes));
        Assert.Contains(UiCopy.RescheduleHint, card);

        var target = choices[0];
        var moved = await PostChangeAsync(customer, upcoming, "Reschedule", bookingId, target);
        var body = await moved.Content.ReadAsStringAsync();
        Assert.True(moved.StatusCode == HttpStatusCode.Redirect, body);
        Assert.Contains($"notice={CustomerNotices.Rescheduled}", moved.Headers.Location?.OriginalString);

        var page = WebUtility.HtmlDecode(await customer.GetStringAsync(moved.Headers.Location));
        Assert.Contains(UiCopy.RescheduledNotice, page);
        var done = Article(page, bookingId);
        Assert.Contains(BookingArticle(bookingId, target, BookingStatuses.Upcoming), done);
        Assert.Contains(PaymentStatuses.Paid, done);
        Assert.DoesNotContain(PaymentStatuses.Refunded, done);
        Assert.Contains(slotId, OptionIds(done));
        Assert.DoesNotContain(target, OptionIds(done));

        var profile = await customer.GetStringAsync(Profile(SessionModes.Online));
        Assert.Contains(slotId.ToString(), profile);
        Assert.DoesNotContain(target.ToString(), profile);
    }

    [Fact]
    public async Task Illegal_cancel_and_reschedule_stay_on_the_page_with_the_api_error()
    {
        await using var web = CreateWeb(_api);
        var customer = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(customer, SessionModes.Online);
        var studio = (await SlotIdsAsync(SessionModes.Studio)).First();

        var pending = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        Assert.Contains("data-action=\"cancel\"", Article(pending, bookingId));
        Assert.DoesNotContain("data-action=\"reschedule\"", Article(pending, bookingId));

        var tooSoon = await PostChangeAsync(customer, pending, "Reschedule", bookingId, studio);
        var tooSoonBody = WebUtility.HtmlDecode(await tooSoon.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, tooSoon.StatusCode);
        Assert.Contains("Only an upcoming booking can be rescheduled.", tooSoonBody);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.PendingAccept), tooSoonBody);

        var missingSlot = await PostChangeAsync(customer, pending, "Reschedule", bookingId);
        var missingBody = WebUtility.HtmlDecode(await missingSlot.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, missingSlot.StatusCode);
        Assert.Contains(UiCopy.RescheduleSlotRequired, missingBody);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.PendingAccept), missingBody);

        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        await AcceptAsync(instructor, bookingId);

        var upcoming = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var sameSlot = await PostChangeAsync(customer, upcoming, "Reschedule", bookingId, slotId);
        var sameBody = WebUtility.HtmlDecode(await sameSlot.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, sameSlot.StatusCode);
        Assert.Contains("Pick a different slot.", sameBody);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), sameBody);
        Assert.Contains(PaymentStatuses.Paid, Article(sameBody, bookingId));

        var otherMode = await PostChangeAsync(customer, upcoming, "Reschedule", bookingId, studio);
        var modeBody = WebUtility.HtmlDecode(await otherMode.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, otherMode.StatusCode);
        Assert.Contains("same session mode", modeBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), modeBody);

        await ShiftSlotToYesterdayAsync(slotId);
        var started = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var startedCard = Article(started, bookingId);
        Assert.DoesNotContain("data-action=\"cancel\"", startedCard);
        Assert.Contains("data-action=\"reschedule\"", startedCard);

        var late = await PostChangeAsync(customer, started, "Cancel", bookingId);
        var lateBody = WebUtility.HtmlDecode(await late.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        Assert.Contains("already started", lateBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), lateBody);
        Assert.Contains(PaymentStatuses.Paid, Article(lateBody, bookingId));

        var listed = WebUtility.HtmlDecode(await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.Upcoming}"));
        var completed = await instructor.PostAsync(
            $"/instructor/bookings?handler=Complete&id={bookingId}&status={BookingStatuses.Upcoming}",
            Form(listed, new Dictionary<string, string>()));
        var completedBody = await completed.Content.ReadAsStringAsync();
        Assert.True(completed.StatusCode == HttpStatusCode.Redirect, completedBody);

        var done = WebUtility.HtmlDecode(await customer.GetStringAsync("/bookings"));
        var doneCard = Article(done, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Completed), doneCard);
        Assert.Contains("data-action=\"review\"", doneCard);
        Assert.DoesNotContain("data-action=\"cancel\"", doneCard);
        Assert.DoesNotContain("data-action=\"reschedule\"", doneCard);

        var cancelDone = await PostChangeAsync(customer, done, "Cancel", bookingId);
        var cancelDoneBody = WebUtility.HtmlDecode(await cancelDone.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, cancelDone.StatusCode);
        Assert.Contains("Only a pending or upcoming booking can be cancelled.", cancelDoneBody);
        Assert.Contains("data-action=\"review\"", cancelDoneBody);

        var moveDone = await PostChangeAsync(customer, done, "Reschedule", bookingId, studio);
        var moveDoneBody = WebUtility.HtmlDecode(await moveDone.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, moveDone.StatusCode);
        Assert.Contains("Only an upcoming booking can be rescheduled.", moveDoneBody);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Completed), moveDoneBody);
    }

    [Fact]
    public async Task Another_customer_cannot_cancel_or_reschedule()
    {
        await using var web = CreateWeb(_api);
        var owner = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(owner, SessionModes.Online);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        await AcceptAsync(instructor, bookingId);
        var target = (await SlotIdsAsync(SessionModes.Online)).First(id => id != slotId);

        var other = await SignInNewAsync(web);
        var empty = await other.GetStringAsync("/bookings");
        Assert.DoesNotContain(bookingId.ToString(), empty);

        var cancel = await PostChangeAsync(other, empty, "Cancel", bookingId);
        var cancelBody = WebUtility.HtmlDecode(await cancel.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Contains("another account", cancelBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(bookingId.ToString(), cancelBody);

        var move = await PostChangeAsync(other, empty, "Reschedule", bookingId, target);
        var moveBody = WebUtility.HtmlDecode(await move.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        Assert.Contains("another account", moveBody, StringComparison.OrdinalIgnoreCase);

        var mine = WebUtility.HtmlDecode(await owner.GetStringAsync("/bookings"));
        var card = Article(mine, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), card);
        Assert.Contains(PaymentStatuses.Paid, card);
        Assert.Contains("data-action=\"cancel\"", card);
    }

    [Fact]
    public async Task Reschedule_to_a_taken_slot_shows_the_conflict()
    {
        await using var web = CreateWeb(_api);
        var first = await SignInNewAsync(web);
        var (slotId, bookingId) = await PayForFutureSlotAsync(first, SessionModes.Online);
        var (instructor, _) = await SignInExistingAsync(web, InstructorPhone);
        await AcceptAsync(instructor, bookingId);

        var second = await SignInNewAsync(web);
        var (takenId, _) = await PayForFutureSlotAsync(second, SessionModes.Online);
        Assert.NotEqual(slotId, takenId);

        var upcoming = WebUtility.HtmlDecode(await first.GetStringAsync("/bookings"));
        Assert.DoesNotContain(takenId.ToString(), OptionIds(Article(upcoming, bookingId)).Select(id => id.ToString()));

        var conflict = await PostChangeAsync(first, upcoming, "Reschedule", bookingId, takenId);
        var body = WebUtility.HtmlDecode(await conflict.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Contains("no longer available", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(UiCopy.RescheduledNotice, body);
        var card = Article(body, bookingId);
        Assert.Contains(BookingArticle(bookingId, slotId, BookingStatuses.Upcoming), card);
        Assert.Contains(PaymentStatuses.Paid, card);
        Assert.DoesNotContain(takenId.ToString(), OptionIds(card).Select(id => id.ToString()));
        Assert.Contains("data-action=\"reschedule\"", card);
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

    private async Task<(Guid SlotId, Guid BookingId)> PayForFutureSlotAsync(HttpClient client, string mode)
    {
        var slotId = (await FutureSlotIdsAsync(mode)).First();
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
        return (slotId, Guid.Parse(booked.Groups[1].Value));
    }

    private async Task<List<Guid>> FutureSlotIdsAsync(string mode)
    {
        var slots = await SlotsAsync(mode);
        var ids = slots
            .Where(slot => !HasStarted(slot))
            .OrderByDescending(slot => slot.Date)
            .ThenByDescending(slot => slot.Start, StringComparer.Ordinal)
            .Select(slot => slot.Id)
            .ToList();
        Assert.NotEmpty(ids);
        return ids;
    }

    private async Task<HashSet<Guid>> SlotIdsAsync(string mode) =>
        (await SlotsAsync(mode)).Select(slot => slot.Id).ToHashSet();

    private async Task<List<SlotBody>> SlotsAsync(string mode)
    {
        var list = await _api.CreateClient().GetFromJsonAsync<SlotListBody>(
            $"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode={Uri.EscapeDataString(mode)}",
            Json);
        Assert.NotNull(list);
        return list!.Slots;
    }

    private async Task ShiftSlotToYesterdayAsync(Guid slotId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var slot = await db.AvailabilitySlots.SingleAsync(s => s.Id == slotId);
        slot.Date = MumbaiClock.Today().AddDays(-1);
        await db.SaveChangesAsync();
    }

    private static async Task AcceptAsync(HttpClient instructor, Guid bookingId)
    {
        var pending = await instructor.GetStringAsync($"/instructor/bookings?status={BookingStatuses.PendingAccept}");
        var accepted = await instructor.PostAsync(
            $"/instructor/bookings?handler=Accept&id={bookingId}&status={BookingStatuses.PendingAccept}",
            Form(pending, new Dictionary<string, string>()));
        var body = await accepted.Content.ReadAsStringAsync();
        Assert.True(accepted.StatusCode == HttpStatusCode.Redirect, body);
    }

    private static async Task<HttpResponseMessage> PostChangeAsync(
        HttpClient client,
        string html,
        string handler,
        Guid bookingId,
        Guid? slotId = null)
    {
        var fields = new Dictionary<string, string>();
        if (slotId is Guid id)
            fields["slotId"] = id.ToString();
        return await client.PostAsync($"/bookings?handler={handler}&id={bookingId}", Form(html, fields));
    }

    private static string Profile(string mode) =>
        $"/instructors/{SeedIds.AnanyaProviderId}?mode={mode}";

    private static string Article(string html, Guid bookingId)
    {
        var marker = $"data-booking-id=\"{bookingId}\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, html);
        var open = html.LastIndexOf("<article", start, StringComparison.OrdinalIgnoreCase);
        var close = html.IndexOf("</article>", start, StringComparison.OrdinalIgnoreCase);
        Assert.True(open >= 0 && close > open, html);
        return html[open..(close + "</article>".Length)];
    }

    private static List<Guid> OptionIds(string article)
    {
        var form = Regex.Match(article, "data-action=\"reschedule\"[\\s\\S]*?</form>", RegexOptions.IgnoreCase);
        if (!form.Success)
            return [];
        return Regex.Matches(form.Value, "<option\\b[^>]*value=\"([0-9a-fA-F-]{36})\"", RegexOptions.IgnoreCase)
            .Select(match => Guid.Parse(match.Groups[1].Value))
            .ToList();
    }

    private static bool HasStarted(SlotBody slot)
    {
        if (!TimeOnly.TryParseExact(slot.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return true;
        return MumbaiClock.SessionStart(slot.Date, start) <= DateTimeOffset.UtcNow;
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
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start, string End);
    private sealed record SlotListBody(string Mode, DateOnly From, DateOnly To, List<SlotBody> Slots);
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using YogaMarketplace.Api.Jobs;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Each test gets its own database, because the job runner sends every due job in it. Jobs are off in Testing, so tests
/// run the runner themselves at chosen times.
/// </summary>
public class NotificationTests
{
    private const string InstructorEmail = "ananya@example.com";
    private const string KeySecret = "dev-only-not-a-live-key-secret";
    private const string WebhookSecret = "dev-only-not-a-live-webhook-secret";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Category_template_wins_over_the_default_and_inactive_ones_are_ignored()
    {
        var category = Guid.NewGuid();
        var fallback = Template(null, "default");
        var special = Template(category, "special");
        var retired = Template(category, "retired", active: false);

        Assert.Same(special, NotificationTemplateRules.Choose([fallback, special], category));
        Assert.Same(fallback, NotificationTemplateRules.Choose([fallback, retired], category));
        Assert.Same(fallback, NotificationTemplateRules.Choose([fallback, special], null));
        Assert.Same(fallback, NotificationTemplateRules.Choose([fallback, special], Guid.NewGuid()));
        Assert.Null(NotificationTemplateRules.Choose([retired], category));
    }

    [Fact]
    public void Render_fills_every_placeholder_and_a_missing_key_is_an_error()
    {
        var data = new Dictionary<string, string> { ["name"] = "Asha", ["when"] = "Mon 5 Oct 2026" };

        Assert.Equal("Hi Asha, see you Mon 5 Oct 2026. Hi Asha", NotificationTemplateRules.Render("Hi {{name}}, see you {{ when }}. Hi {{name}}", data));
        Assert.Equal("No placeholders {here}", NotificationTemplateRules.Render("No placeholders {here}", data));
        var missing = Assert.Throws<KeyNotFoundException>(() => NotificationTemplateRules.Render("Refund {{refund}}", data));
        Assert.Contains("{{refund}}", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subject_values_lose_their_line_breaks_but_the_body_keeps_them()
    {
        var data = new Dictionary<string, string> { ["name"] = "Asha\r\nBcc: victim@example.com\nX" };

        var subject = NotificationTemplateRules.Render("Booked by {{name}}", data, singleLine: true);

        Assert.Equal("Booked by Asha Bcc: victim@example.com X", subject);
        Assert.DoesNotContain('\r', subject);
        Assert.DoesNotContain('\n', subject);
        Assert.Contains("\r\n", NotificationTemplateRules.Render("{{name}}", data), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false, 899, "INR 899.00")]
    [InlineData(true, true, 0, "1 credit back in your pack")]
    [InlineData(true, true, 49, "INR 49.00, and 1 credit back in your pack")]
    [InlineData(true, false, 0, "None. The credit for this session stays used")]
    [InlineData(true, false, 49, "INR 49.00. The credit for this session stays used")]
    public void Refund_text_names_the_credit_for_credit_paid_bookings(bool paidWithCredit, bool creditReturned, int refund, string expected)
    {
        Assert.Equal(expected, BookingNotifications.RefundText("INR", refund, paidWithCredit, creditReturned));
    }

    [Fact]
    public async Task Default_templates_cover_every_event_without_naming_a_seeded_city_or_area()
    {
        await using var factory = new YogaApiFactory();
        var templates = await QueryAsync(factory, db => db.NotificationTemplates.AsNoTracking().ToListAsync());
        var areas = await QueryAsync(factory, db => db.Areas.Include(a => a.City).AsNoTracking().ToListAsync());
        var places = areas.Select(a => a.City!.Name).Concat(areas.Select(a => a.Name)).Distinct().ToList();

        var events = typeof(NotificationEvents).GetFields().Select(f => (string)f.GetValue(null)!).ToList();
        Assert.All(events, e => Assert.Single(templates, t => t.EventKey == e && t.CategoryId is null && t.IsActive));
        Assert.NotEmpty(places);
        Assert.All(templates, t => Assert.All(places, place =>
            Assert.DoesNotContain(place, t.Subject + t.Body, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Booking_request_is_queued_with_the_booking_and_sent_by_the_runner_when_the_flag_is_on()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, _) = await SignUpAsync(host);

        var booking = await BookAsync(host, customer);

        var rows = await OutboxAsync(host);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(NotificationStatus.Pending, r.Status));
        var toCustomer = Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingRequestedCustomer);
        var toInstructor = Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingRequestedInstructor);
        Assert.Null(toCustomer.Recipient);
        Assert.Equal(InstructorEmail, toInstructor.Recipient);
        Assert.Equal(SeedIds.YogaCategoryId, toInstructor.CategoryId);
        Assert.Equal(2, await CountJobsAsync(host));

        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal(InstructorEmail, sent.To);
        Assert.StartsWith("New booking request:", sent.Subject, StringComparison.Ordinal);
        Assert.Contains(booking.Id.ToString("N")[..8].ToUpperInvariant(), sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", sent.Subject + sent.Body, StringComparison.Ordinal);
        rows = await OutboxAsync(host);
        var instructorRow = rows.Single(r => r.Id == toInstructor.Id);
        Assert.Equal(NotificationStatus.Sent, instructorRow.Status);
        Assert.Equal(1, instructorRow.Attempts);
        Assert.NotNull(instructorRow.SentAt);
        var customerRow = rows.Single(r => r.Id == toCustomer.Id);
        Assert.Equal(NotificationStatus.Skipped, customerRow.Status);
        Assert.Equal("No email on file.", customerRow.LastError);
    }

    [Fact]
    public async Task A_pending_booking_gets_request_wording_and_accepting_it_sends_the_confirmation()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, phone) = await SignUpAsync(host);
        await QueryAsync(host, db => db.Users.Where(u => u.Phone == phone).ExecuteUpdateAsync(u => u.SetProperty(x => x.Email, "asha@example.com")));
        var instructor = await SignInAsync(host, SeedIds.AnanyaPhone);

        var booking = await BookAsync(host, customer);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var requested = sender.Sent.ToList();
        Assert.Equal(2, requested.Count);
        Assert.StartsWith("Request received:", requested.Single(m => m.To == "asha@example.com").Subject, StringComparison.Ordinal);
        Assert.StartsWith("New booking request:", requested.Single(m => m.To == InstructorEmail).Subject, StringComparison.Ordinal);
        Assert.All(requested, m => Assert.DoesNotContain("confirmed", m.Subject + m.Body, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await CountRowsAsync(host, NotificationEvents.BookingConfirmedCustomer));

        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booking.Id}/accept", new { });
        Assert.Equal(HttpStatusCode.Conflict, (await instructor.PostAsJsonAsync($"/api/bookings/{booking.Id}/accept", new { })).StatusCode);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(1, await CountRowsAsync(host, NotificationEvents.BookingConfirmedCustomer));
        Assert.Equal(1, await CountRowsAsync(host, NotificationEvents.BookingConfirmedInstructor));
        var confirmed = sender.Sent.Skip(2).ToList();
        Assert.Equal(2, confirmed.Count);
        Assert.StartsWith("Booking confirmed:", confirmed.Single(m => m.To == InstructorEmail).Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("request", confirmed.Single(m => m.To == "asha@example.com").Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_credit_booking_gets_request_wording_and_accepting_it_sends_the_confirmation()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var admin = await SignInAsync(host, SeedIds.AdminPhone);
        var instructor = await SignInAsync(host, SeedIds.AnanyaPhone);
        var pack = await PostAsync<IdBody>(admin, "/api/admin/packs",
            new { name = "Notify pack " + Guid.NewGuid().ToString("N")[..8], sessions = 2, price = 1600m, validityDays = 30, priceCap = 5000m });
        var (customer, phone) = await SignUpAsync(host);
        await QueryAsync(host, db => db.Users.Where(u => u.Phone == phone).ExecuteUpdateAsync(u => u.SetProperty(x => x.Email, "asha@example.com")));
        var packOrder = await PostAsync<OrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        await PostAsync<JsonElement>(customer, "/api/packs/local-confirm", new { orderId = packOrder.OrderId });

        var booking = await PostAsync<BookingBody>(customer, "/api/bookings/credit", new { slotId = await OpenSlotAsync(customer) });
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var requested = sender.Sent.ToList();
        Assert.Equal(2, requested.Count);
        Assert.StartsWith("Request received:", requested.Single(m => m.To == "asha@example.com").Subject, StringComparison.Ordinal);
        Assert.StartsWith("New booking request:", requested.Single(m => m.To == InstructorEmail).Subject, StringComparison.Ordinal);
        Assert.All(requested, m => Assert.DoesNotContain("confirmed", m.Subject + m.Body, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await CountRowsAsync(host, NotificationEvents.BookingConfirmedCustomer));

        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booking.Id}/accept", new { });
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(1, await CountRowsAsync(host, NotificationEvents.BookingConfirmedCustomer));
        Assert.Equal(1, await CountRowsAsync(host, NotificationEvents.BookingConfirmedInstructor));
        var confirmed = sender.Sent.Skip(2).ToList();
        Assert.Equal(2, confirmed.Count);
        Assert.StartsWith("Booking confirmed:", confirmed.Single(m => m.To == InstructorEmail).Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("request", confirmed.Single(m => m.To == "asha@example.com").Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recipient_email_and_phone_are_masked_in_logs()
    {
        await using var factory = new YogaApiFactory();
        var (host, _, logs) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, phone) = await SignUpAsync(host);

        await BookAsync(host, customer);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var lines = logs.Lines.ToList();
        Assert.Contains(lines, l => l.Contains("Sent notification", StringComparison.Ordinal) && l.Contains("***@example.com", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Queued notification", StringComparison.Ordinal) && l.Contains(phone[^4..], StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains(InstructorEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, l => l.Contains(phone[3..], StringComparison.Ordinal));
    }

    [Fact]
    public async Task Flag_off_marks_rows_skipped_without_sending_or_failing()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        var (customer, _) = await SignUpAsync(host);

        await BookAsync(host, customer);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Empty(sender.Sent);
        Assert.All(await OutboxAsync(host), r =>
        {
            Assert.Equal(NotificationStatus.Skipped, r.Status);
            Assert.Equal("Email notifications are off in admin settings.", r.LastError);
        });
        Assert.All(await JobsAsync(host), j => Assert.Equal(BackgroundJobStatus.Succeeded, j.Status));
    }

    [Fact]
    public async Task Missing_smtp_marks_rows_skipped()
    {
        await using var factory = new YogaApiFactory();
        await PatchSettingsAsync(factory, new { emailNotificationsEnabled = true });
        var (customer, _) = await SignUpAsync(factory);

        await BookAsync(factory, customer);
        await RunJobsAsync(factory, DateTimeOffset.UtcNow.AddSeconds(1));

        var instructorRow = Assert.Single(await OutboxAsync(factory), r => r.Recipient == InstructorEmail);
        Assert.Equal(NotificationStatus.Skipped, instructorRow.Status);
        Assert.Equal("SMTP is not configured.", instructorRow.LastError);
        var job = await JobForAsync(factory, instructorRow.Id);
        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        Assert.Null(job.LastError);
    }

    [Fact]
    public async Task Tokens_in_a_sender_error_are_masked_in_the_stored_error_text()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJl";
        const string bearer = "Bearer sk_live_relay_7f3a9c";
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, _) = await SignUpAsync(host);
        await BookAsync(host, customer);
        sender.FailuresLeft = 1;
        sender.FailureText = $"Relay rejected {bearer} and session {jwt}.";

        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var row = await InstructorRowAsync(host);
        var job = await JobForAsync(host, row.Id);
        foreach (var stored in new[] { row.LastError, job.LastError })
        {
            Assert.NotNull(stored);
            Assert.Contains("Bearer [token]", stored, StringComparison.Ordinal);
            Assert.Contains("[jwt]", stored, StringComparison.Ordinal);
            Assert.DoesNotContain("sk_live_relay_7f3a9c", stored, StringComparison.Ordinal);
            Assert.DoesNotContain(jwt, stored, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Sender_failure_is_retried_with_the_runner_backoff_and_then_sent()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, _) = await SignUpAsync(host);
        await BookAsync(host, customer);
        sender.FailuresLeft = 1;

        var now = WholeSecond(DateTimeOffset.UtcNow).AddSeconds(1);
        await RunJobsAsync(host, now);

        var row = await InstructorRowAsync(host);
        Assert.Equal(NotificationStatus.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Contains("***@example.com", row.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain(InstructorEmail, row.LastError, StringComparison.Ordinal);
        var job = await JobForAsync(host, row.Id);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(now + BackgroundJobRunner.BackoffFor(1), job.RunAt);
        Assert.DoesNotContain(InstructorEmail, job.LastError, StringComparison.Ordinal);

        await RunJobsAsync(host, job.RunAt.AddSeconds(1));

        row = await InstructorRowAsync(host);
        Assert.Equal(NotificationStatus.Sent, row.Status);
        Assert.Equal(2, row.Attempts);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task Sender_failure_on_the_last_admin_set_attempt_fails_the_row_and_the_job()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true, notificationMaxAttempts = 2 });
        var (customer, _) = await SignUpAsync(host);
        await BookAsync(host, customer);
        sender.FailuresLeft = int.MaxValue;

        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));
        var row = await InstructorRowAsync(host);
        Assert.Equal(2, row.MaxAttempts);
        await RunJobsAsync(host, (await JobForAsync(host, row.Id)).RunAt.AddSeconds(1));

        row = await InstructorRowAsync(host);
        Assert.Equal(NotificationStatus.Failed, row.Status);
        Assert.Equal(2, row.Attempts);
        Assert.Equal(BackgroundJobStatus.Failed, (await JobForAsync(host, row.Id)).Status);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Nothing_is_queued_when_the_booking_save_rolls_back()
    {
        await using var factory = new YogaApiFactory();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IInterceptor, FailBookingInsert>()));
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        var (customer, _) = await SignUpAsync(host);

        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId = await OpenSlotAsync(customer) });
        var response = await customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = order.OrderId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await OutboxAsync(host));
        Assert.Equal(0, await CountJobsAsync(host));
    }

    [Fact]
    public async Task Category_template_overrides_the_default()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        await AddTemplateAsync(host, "Yoga booking: {{serviceTitle}} with {{customerName}}", "See you on {{sessionDate}}.");
        var (customer, _) = await SignUpAsync(host);

        await BookAsync(host, customer);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var sent = Assert.Single(sender.Sent);
        Assert.StartsWith("Yoga booking:", sent.Subject, StringComparison.Ordinal);
        Assert.StartsWith("See you on", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Template_with_a_missing_placeholder_fails_the_row_without_retrying()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true });
        await AddTemplateAsync(host, "Booked {{serviceTitle}}", "Your mat number is {{matNumber}}.");
        var (customer, _) = await SignUpAsync(host);

        await BookAsync(host, customer);
        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));

        var row = await InstructorRowAsync(host);
        Assert.Equal(NotificationStatus.Failed, row.Status);
        Assert.Contains("{{matNumber}}", row.LastError, StringComparison.Ordinal);
        Assert.Equal(BackgroundJobStatus.Succeeded, (await JobForAsync(host, row.Id)).Status);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Customer_cancel_and_instructor_decline_queue_cancelled_and_refunded_emails()
    {
        await using var factory = new YogaApiFactory();
        var (customer, _) = await SignUpAsync(factory);
        var instructor = await SignInAsync(factory, SeedIds.AnanyaPhone);

        var cancelled = await BookAsync(factory, customer);
        await PostAsync<BookingBody>(customer, $"/api/bookings/{cancelled.Id}/cancel", new { });
        var declined = await BookAsync(factory, customer);
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{declined.Id}/decline", new { });

        var rows = await OutboxAsync(factory);
        var cancelledRow = Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingCancelledCustomer);
        Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingCancelledInstructor);
        var refundedRow = Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingRefundedCustomer);
        Assert.Single(rows, r => r.EventKey == NotificationEvents.BookingRefundedInstructor);
        Assert.Matches("^INR [1-9][0-9]*\\.[0-9]{2}$", Data(cancelledRow)["refund"]);
        Assert.Matches("^INR [1-9][0-9]*\\.[0-9]{2}$", Data(refundedRow)["refund"]);
    }

    [Fact]
    public async Task Notification_settings_are_validated_and_audited()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);

        await PatchSettingsAsync(factory, new { emailNotificationsEnabled = true, notificationMaxAttempts = 3 });
        Assert.Equal(HttpStatusCode.BadRequest, (await PatchAsync(admin, new { notificationMaxAttempts = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PatchAsync(admin, new { notificationMaxAttempts = 11 })).StatusCode);

        var saved = await admin.GetFromJsonAsync<NotificationSettingsBody>("/api/admin/settings", Json);
        Assert.True(saved!.EmailNotificationsEnabled);
        Assert.Equal(3, saved.NotificationMaxAttempts);
        var audit = await admin.GetFromJsonAsync<List<AuditBody>>("/api/admin/settings/audit", Json);
        Assert.Contains(audit!, a => a is { Field: "EmailNotificationsEnabled", OldValue: "False", NewValue: "True" });
        Assert.Contains(audit!, a => a is { Field: "NotificationMaxAttempts", OldValue: "5", NewValue: "3" });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Notification_attempts_accept_the_range_ends(int attempts)
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);

        await PatchSettingsAsync(factory, new { notificationMaxAttempts = attempts });

        Assert.Equal(attempts, (await admin.GetFromJsonAsync<NotificationSettingsBody>("/api/admin/settings", Json))!.NotificationMaxAttempts);
    }

    [Fact]
    public async Task Saving_notification_settings_with_a_stale_version_is_a_conflict()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
        var stale = (await admin.GetFromJsonAsync<VersionBody>("/api/admin/settings", Json))!.Version;
        await PatchSettingsAsync(factory, new { notificationMaxAttempts = 4 });

        var response = await admin.PatchAsJsonAsync("/api/admin/settings", new { version = stale, notificationMaxAttempts = 7 }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(4, (await admin.GetFromJsonAsync<NotificationSettingsBody>("/api/admin/settings", Json))!.NotificationMaxAttempts);
    }

    [Fact]
    public async Task Error_outside_the_send_retries_and_then_fails_the_row_instead_of_leaving_it_pending()
    {
        await using var factory = new YogaApiFactory();
        var (host, sender, _) = NewHost(factory);
        await PatchSettingsAsync(host, new { emailNotificationsEnabled = true, notificationMaxAttempts = 2 });
        var (customer, _) = await SignUpAsync(host);
        await BookAsync(host, customer);
        var rowId = (await InstructorRowAsync(host)).Id;
        await QueryAsync(host, db => db.NotificationOutbox.Where(r => r.Id == rowId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Data, "not json")));

        await RunJobsAsync(host, DateTimeOffset.UtcNow.AddSeconds(1));
        var row = await InstructorRowAsync(host);
        Assert.Equal((NotificationStatus.Pending, 1), (row.Status, row.Attempts));
        Assert.False(string.IsNullOrEmpty(row.LastError));
        await RunJobsAsync(host, (await JobForAsync(host, rowId)).RunAt.AddSeconds(1));

        row = await InstructorRowAsync(host);
        Assert.Equal((NotificationStatus.Failed, 2), (row.Status, row.Attempts));
        Assert.Equal(BackgroundJobStatus.Failed, (await JobForAsync(host, rowId)).Status);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Replayed_capture_webhook_and_the_client_callback_queue_one_confirmation_pair()
    {
        await using var factory = new YogaApiFactory();
        var (customer, _) = await SignUpAsync(factory);
        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId = await OpenSlotAsync(customer) });
        var paymentId = "pay_replay_" + Guid.NewGuid().ToString("N");
        var capture = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
            + "\",\"order_id\":\"" + order.OrderId + "\",\"status\":\"captured\",\"amount\":" + order.AmountPaise + ",\"currency\":\"INR\"}}}}";

        var eventId = "evt_" + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, capture, eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, capture, eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, capture, "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);
        await PostAsync<BookingBody>(customer, "/api/bookings/confirm",
            new { orderId = order.OrderId, paymentId, signature = Sign(KeySecret, $"{order.OrderId}|{paymentId}") });

        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRequestedCustomer));
        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRequestedInstructor));
        Assert.Equal(2, await CountJobsAsync(factory));
    }

    [Fact]
    public async Task Repeating_a_cancel_or_decline_queues_no_second_pair()
    {
        await using var factory = new YogaApiFactory();
        var (customer, _) = await SignUpAsync(factory);
        var instructor = await SignInAsync(factory, SeedIds.AnanyaPhone);
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);

        var cancelled = await BookAsync(factory, customer);
        await PostAsync<BookingBody>(customer, $"/api/bookings/{cancelled.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync($"/api/bookings/{cancelled.Id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/bookings/{cancelled.Id}/cancel", new { reason = "Again" })).StatusCode);
        var declined = await BookAsync(factory, customer);
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{declined.Id}/decline", new { });
        Assert.Equal(HttpStatusCode.Conflict, (await instructor.PostAsJsonAsync($"/api/bookings/{declined.Id}/decline", new { })).StatusCode);

        foreach (var key in new[] { NotificationEvents.BookingCancelledCustomer, NotificationEvents.BookingCancelledInstructor,
                     NotificationEvents.BookingRefundedCustomer, NotificationEvents.BookingRefundedInstructor })
            Assert.Equal(1, await CountRowsAsync(factory, key));
    }

    [Fact]
    public async Task Dashboard_refund_webhook_queues_one_refunded_pair_however_often_it_is_delivered()
    {
        await using var factory = new YogaApiFactory();
        var (customer, _) = await SignUpAsync(factory);
        var booking = await BookAsync(factory, customer);
        var payment = await QueryAsync(factory, db => db.Payments.AsNoTracking().SingleAsync(p => p.BookingId == booking.Id));
        var refund = RefundBody("rfnd_dash_" + Guid.NewGuid().ToString("N"), payment.GatewayPaymentId!, (long)(payment.Amount * 100));

        var eventId = "evt_" + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, refund, eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, refund, eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, refund, "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);

        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRefundedCustomer));
        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRefundedInstructor));
        Assert.Equal(BookingStatus.Cancelled, await QueryAsync(factory, db => db.Bookings.Where(b => b.Id == booking.Id).Select(b => b.Status).SingleAsync()));
    }

    [Fact]
    public async Task Refund_webhook_for_our_own_cancel_adds_no_refunded_emails()
    {
        await using var factory = new YogaApiFactory();
        var (customer, _) = await SignUpAsync(factory);
        var booking = await BookAsync(factory, customer);
        await PostAsync<BookingBody>(customer, $"/api/bookings/{booking.Id}/cancel", new { });
        var payment = await QueryAsync(factory, db => db.Payments.AsNoTracking().SingleAsync(p => p.BookingId == booking.Id));

        var refund = RefundBody(payment.GatewayRefundId!, payment.GatewayPaymentId!, (long)(payment.RefundedAmount * 100));
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, refund, "evt_" + Guid.NewGuid().ToString("N"))).StatusCode);

        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingCancelledCustomer));
        Assert.Equal(0, await CountRowsAsync(factory, NotificationEvents.BookingRefundedCustomer));
    }

    [Fact]
    public async Task Nothing_is_queued_when_a_cancel_decline_or_admin_cancel_save_rolls_back()
    {
        await using var factory = new YogaApiFactory();
        var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IInterceptor, FailBookingOrPaymentUpdate>()));
        var (customer, phone) = await SignUpAsync(factory);
        var booking = await BookAsync(factory, customer);
        var failingCustomer = await SignInAsync(failing, phone);

        Assert.Equal(HttpStatusCode.Conflict, (await failingCustomer.PostAsJsonAsync($"/api/bookings/{booking.Id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await (await SignInAsync(failing, SeedIds.AnanyaPhone))
            .PostAsJsonAsync($"/api/bookings/{booking.Id}/decline", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await (await SignInAsync(failing, SeedIds.AdminPhone))
            .PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/cancel", new { reason = "Instructor unwell" })).StatusCode);

        var rows = await OutboxAsync(factory);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.StartsWith("booking.requested.", r.EventKey, StringComparison.Ordinal));
        Assert.Equal(2, await CountJobsAsync(factory));
    }

    [Fact]
    public async Task A_dashboard_refund_that_rolls_back_queues_nothing_and_its_redelivery_queues_one_pair()
    {
        await using var factory = new YogaApiFactory();
        var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IInterceptor, FailBookingOrPaymentUpdate>()));
        var (customer, _) = await SignUpAsync(factory);
        var booking = await BookAsync(factory, customer);
        var payment = await QueryAsync(factory, db => db.Payments.AsNoTracking().SingleAsync(p => p.BookingId == booking.Id));
        var refund = RefundBody("rfnd_dash_" + Guid.NewGuid().ToString("N"), payment.GatewayPaymentId!, (long)(payment.Amount * 100));
        var eventId = "evt_" + Guid.NewGuid().ToString("N");

        Assert.Equal(HttpStatusCode.Conflict, (await PostWebhookAsync(failing, refund, eventId)).StatusCode);
        Assert.Equal(0, await CountRowsAsync(factory, NotificationEvents.BookingRefundedCustomer));

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(factory, refund, eventId)).StatusCode);
        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRefundedCustomer));
        Assert.Equal(1, await CountRowsAsync(factory, NotificationEvents.BookingRefundedInstructor));
    }

    [Fact]
    public async Task Credit_paid_cancel_and_decline_name_the_returned_credit()
    {
        await using var factory = new YogaApiFactory();
        var admin = await SignInAsync(factory, SeedIds.AdminPhone);
        var instructor = await SignInAsync(factory, SeedIds.AnanyaPhone);
        var pack = await PostAsync<IdBody>(admin, "/api/admin/packs",
            new { name = "Notify pack " + Guid.NewGuid().ToString("N")[..8], sessions = 2, price = 1600m, validityDays = 30, priceCap = 5000m });
        var (customer, _) = await SignUpAsync(factory);
        var packOrder = await PostAsync<OrderBody>(customer, $"/api/packs/{pack.Id}/orders", new { });
        await PostAsync<JsonElement>(customer, "/api/packs/local-confirm", new { orderId = packOrder.OrderId });

        var cancelled = await PostAsync<BookingBody>(customer, "/api/bookings/credit", new { slotId = await OpenSlotAsync(customer) });
        await PostAsync<BookingBody>(customer, $"/api/bookings/{cancelled.Id}/cancel", new { });
        var declined = await PostAsync<BookingBody>(customer, "/api/bookings/credit", new { slotId = await OpenSlotAsync(customer) });
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{declined.Id}/decline", new { });

        var rows = await OutboxAsync(factory);
        Assert.Equal("1 credit back in your pack", Data(rows.Single(r => r.EventKey == NotificationEvents.BookingCancelledCustomer))["refund"]);
        Assert.Equal("1 credit back in your pack", Data(rows.Single(r => r.EventKey == NotificationEvents.BookingRefundedCustomer))["refund"]);
    }

    /// <summary>SQLite stores job times as binary and drops sub-millisecond ticks, so exact comparisons use whole seconds.</summary>
    private static DateTimeOffset WholeSecond(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    private static NotificationTemplate Template(Guid? categoryId, string subject, bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = NotificationEvents.BookingConfirmedCustomer,
        CategoryId = categoryId,
        Subject = subject,
        Body = subject,
        IsActive = active
    };

    private static (WebApplicationFactory<Program> Host, RecordingSender Sender, LogCapture Logs) NewHost(YogaApiFactory factory)
    {
        var sender = new RecordingSender();
        var logs = new LogCapture();
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IAlertSender>(sender)));
        });
        return (host, sender, logs);
    }

    private static async Task AddTemplateAsync(WebApplicationFactory<Program> host, string subject, string body)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            EventKey = NotificationEvents.BookingRequestedInstructor,
            Channel = NotificationChannel.Email,
            CategoryId = SeedIds.YogaCategoryId,
            Subject = subject,
            Body = body,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task<BookingBody> BookAsync(WebApplicationFactory<Program> host, HttpClient customer)
    {
        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId = await OpenSlotAsync(customer) });
        return await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = order.OrderId });
    }

    private static async Task<Guid> OpenSlotAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<SlotListBody>($"/api/providers/{SeedIds.AnanyaProviderId}/slots?mode=Online", Json);
        return list!.Slots.OrderByDescending(s => s.Date).ThenByDescending(s => s.Start).First().Id;
    }

    private static async Task RunJobsAsync(WebApplicationFactory<Program> host, DateTimeOffset now)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunDueAsync(now, CancellationToken.None);
    }

    private static async Task<T> QueryAsync<T>(WebApplicationFactory<Program> host, Func<YogaDbContext, Task<T>> query)
    {
        using var scope = host.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static Task<List<NotificationOutbox>> OutboxAsync(WebApplicationFactory<Program> host) =>
        QueryAsync(host, db => db.NotificationOutbox.AsNoTracking().ToListAsync());

    private static Task<NotificationOutbox> InstructorRowAsync(WebApplicationFactory<Program> host) =>
        QueryAsync(host, db => db.NotificationOutbox.AsNoTracking().SingleAsync(r => r.Recipient == InstructorEmail));

    private static Task<List<BackgroundJob>> JobsAsync(WebApplicationFactory<Program> host) =>
        QueryAsync(host, db => db.BackgroundJobs.AsNoTracking().Where(j => j.Type == NotificationSendJob.JobType).ToListAsync());

    private static async Task<int> CountJobsAsync(WebApplicationFactory<Program> host) => (await JobsAsync(host)).Count;

    private static Task<BackgroundJob> JobForAsync(WebApplicationFactory<Program> host, Guid outboxId) =>
        QueryAsync(host, db => db.BackgroundJobs.AsNoTracking().SingleAsync(j => j.Payload == outboxId.ToString()));

    private static Task<int> CountRowsAsync(WebApplicationFactory<Program> host, string eventKey) =>
        QueryAsync(host, db => db.NotificationOutbox.CountAsync(r => r.EventKey == eventKey));

    private static Task<HttpResponseMessage> PostWebhookAsync(WebApplicationFactory<Program> host, string body, string eventId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/razorpay")
        {
            Content = new StringContent(body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Sign(WebhookSecret, body));
        request.Headers.TryAddWithoutValidation("X-Razorpay-Event-Id", eventId);
        return host.CreateClient().SendAsync(request);
    }

    private static string RefundBody(string refundId, string paymentId, long paise) =>
        "{\"event\":\"refund.processed\",\"payload\":{\"refund\":{\"entity\":{\"id\":\"" + refundId
        + "\",\"payment_id\":\"" + paymentId + "\",\"amount\":" + paise + ",\"currency\":\"INR\"}}}}";

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static Dictionary<string, string> Data(NotificationOutbox row) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(row.Data)!;

    private static async Task PatchSettingsAsync(WebApplicationFactory<Program> host, object changes)
    {
        var saved = await PatchAsync(await SignInAsync(host, SeedIds.AdminPhone), changes);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PatchAsync(HttpClient admin, object changes)
    {
        var current = await admin.GetFromJsonAsync<VersionBody>("/api/admin/settings", Json);
        var body = JsonSerializer.SerializeToNode(changes, Json)!.AsObject();
        body["version"] = current!.Version;
        return await admin.PatchAsJsonAsync("/api/admin/settings", body, Json);
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string phone)
    {
        var client = host.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        return await VerifyAsync(client, phone, otp.DevCode);
    }

    private static async Task<(HttpClient Client, string Phone)> SignUpAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var phone = "+9197" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request",
            new { phone, name = "Notified Customer", gender = "Female", isNewUser = true, acceptedPolicies = true });
        var signedIn = await VerifyAsync(client, phone, otp.DevCode);
        await TestPreferredCity.PreferHarborAsync(signedIn);
        return (signedIn, phone);
    }

    private static async Task<HttpClient> VerifyAsync(HttpClient client, string phone, string code)
    {
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record SentEmail(string To, string Subject, string Body);

    /// <summary>Records emails. While <see cref="FailuresLeft"/> is above zero, each send fails like an SMTP outage that echoes the address.</summary>
    private sealed class RecordingSender : IAlertSender
    {
        public ConcurrentQueue<SentEmail> Sent { get; } = new();
        public int FailuresLeft { get; set; }
        public string? FailureText { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new SmtpException(SmtpStatusCode.MailboxBusy, FailureText ?? $"Mailbox busy for <{to}>.");
            }
            Sent.Enqueue(new SentEmail(to, subject, body));
            return Task.CompletedTask;
        }
    }

    /// <summary>Fails any save that inserts a booking, the way a lost slot race does.</summary>
    private sealed class FailBookingInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Booking>().Any(e => e.State == EntityState.Added))
                throw new DbUpdateException("Forced booking insert failure.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Fails any save that changes a booking or payment, the way a lost concurrency race does.</summary>
    private sealed class FailBookingOrPaymentUpdate : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var tracker = eventData.Context!.ChangeTracker;
            if (tracker.Entries<Booking>().Any(e => e.State == EntityState.Modified)
                || tracker.Entries<Payment>().Any(e => e.State == EntityState.Modified))
                throw new DbUpdateConcurrencyException("Forced booking update conflict.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Every log line as "level message | properties | exception", scope properties included.</summary>
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

    private sealed record SlotBody(Guid Id, DateOnly Date, string Start);
    private sealed record SlotListBody(List<SlotBody> Slots);
    private sealed record OrderBody(string OrderId, long AmountPaise);
    private sealed record IdBody(Guid Id);
    private sealed record BookingBody(Guid Id, decimal Amount);
    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record VersionBody(int Version);
    private sealed record AuditBody(string Field, string? OldValue, string? NewValue);
    private sealed record NotificationSettingsBody(bool EmailNotificationsEnabled, int NotificationMaxAttempts);
}

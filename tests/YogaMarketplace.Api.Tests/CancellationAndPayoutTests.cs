using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class CancellationAndPayoutTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public CancellationAndPayoutTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Late_cancel_refunds_the_rest_and_pays_the_instructor_the_kept_fee()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        await SetCancelWindowAsync(admin, 48);
        try
        {
            var customer = await SignUpAsync("Pooja Rane", "Female");
            var instructor = await SignInAsync("9876543210");
            var slotId = await TomorrowSlotAsync(instructor, "Studio", "05:05");
            var booked = await BookAsync(customer, slotId, "pay_late_cancel");

            var pendingQuote = await customer.GetFromJsonAsync<QuoteBody>($"/api/bookings/{booked.Id}/cancel-quote", Json);
            Assert.Equal(0m, pendingQuote!.LateCancelFee);
            Assert.Equal(booked.Amount, pendingQuote.Refund);
            Assert.Null(pendingQuote.FreeUntil);

            await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
            var quote = await customer.GetFromJsonAsync<QuoteBody>($"/api/bookings/{booked.Id}/cancel-quote", Json);
            Assert.Equal(374.50m, quote!.LateCancelFee);
            Assert.Equal(374.50m, quote.Refund);
            Assert.Equal(50m, quote.LateCancelFeePercent);
            Assert.NotNull(quote.FreeUntil);
            Assert.True(quote.FreeUntil < DateTimeOffset.UtcNow);

            var cancelled = await PostAsync<BookingBody>(customer, $"/api/bookings/{booked.Id}/cancel", new { });
            Assert.Equal("Cancelled", cancelled.Status);
            Assert.Equal("PartiallyRefunded", cancelled.PaymentStatus);
            Assert.Equal(374.50m, cancelled.RefundedAmount);
            Assert.Equal(374.50m, cancelled.LateCancelFee);
            Assert.Equal("Customer", cancelled.CancelledBy);

            var refund = Assert.Single(RefundsFor(booked.GatewayPaymentId!));
            Assert.Equal(37450, refund.AmountPaise);

            var payouts = await instructor.GetFromJsonAsync<List<InstructorPayoutBody>>("/api/providers/me/payouts", Json);
            var payout = Assert.Single(payouts!, p => p.BookingId == booked.Id);
            Assert.Equal(374.50m, payout.GrossAmount);
            Assert.Equal(56.18m, payout.FeeAmount);
            Assert.Equal(318.32m, payout.NetAmount);
            Assert.Equal("Cancelled", payout.BookingStatus);

            var afterQuote = await customer.GetAsync($"/api/bookings/{booked.Id}/cancel-quote");
            Assert.Equal(HttpStatusCode.BadRequest, afterQuote.StatusCode);
        }
        finally
        {
            await SetCancelWindowAsync(admin, 12);
        }
    }

    [Fact]
    public async Task Admin_force_cancel_always_refunds_in_full_and_needs_a_reason()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        await SetCancelWindowAsync(admin, 48);
        try
        {
            var customer = await SignUpAsync("Sana Qureshi", "Female");
            var instructor = await SignInAsync("9876543210");
            var slotId = await TomorrowSlotAsync(instructor, "Studio", "05:25");
            var booked = await BookAsync(customer, slotId, "pay_admin_cancel");
            await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });

            foreach (var caller in new[] { customer, instructor })
            {
                var forbidden = await caller.PostAsJsonAsync($"/api/admin/bookings/{booked.Id}/cancel", new { reason = "Not allowed here" });
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            }

            var noReason = await admin.PostAsJsonAsync($"/api/admin/bookings/{booked.Id}/cancel", new { reason = "  " });
            Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
            Assert.Empty(RefundsFor(booked.GatewayPaymentId!));

            var cancelled = await PostAsync<AdminBookingBody>(
                admin,
                $"/api/admin/bookings/{booked.Id}/cancel",
                new { reason = "Instructor is unwell" });
            Assert.Equal("Cancelled", cancelled.Status);
            Assert.Equal("Refunded", cancelled.PaymentStatus);
            Assert.Equal(booked.Amount, cancelled.RefundedAmount);
            Assert.Null(cancelled.LateCancelFee);
            Assert.Equal("Admin", cancelled.CancelledBy);
            Assert.Equal("Instructor is unwell", cancelled.CancelReason);
            Assert.Equal(RazorpayMoney.ToPaise(booked.Amount), Assert.Single(RefundsFor(booked.GatewayPaymentId!)).AmountPaise);
            Assert.Equal(0, await PayoutCountAsync(booked.Id));

            var mine = await customer.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
            Assert.Contains(mine!, b => b.Id == booked.Id && b.CancelledBy == "Admin" && b.CancelReason == "Instructor is unwell");

            var again = await admin.PostAsJsonAsync($"/api/admin/bookings/{booked.Id}/cancel", new { reason = "Second attempt" });
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Single(RefundsFor(booked.GatewayPaymentId!));

            var missing = await admin.PostAsJsonAsync($"/api/admin/bookings/{Guid.NewGuid()}/cancel", new { reason = "Unknown booking" });
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            await SetCancelWindowAsync(admin, 12);
        }
    }

    [Fact]
    public async Task Payout_export_marks_pending_as_exported_and_returns_a_csv()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var customer = await SignUpAsync("Kavya Jain", "Female");
        var instructor = await SignInAsync("9876543210");
        var slotId = await TomorrowSlotAsync(instructor, "Online", "05:45");
        var booked = await BookAsync(customer, slotId, "pay_export_csv");
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
        await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/complete", new { });
        var payoutId = await PayoutIdAsync(booked.Id);

        var customerExport = await customer.PostAsync("/api/admin/payouts/export", null);
        Assert.Equal(HttpStatusCode.Forbidden, customerExport.StatusCode);

        var preview = await admin.GetAsync("/api/admin/payouts/csv?status=Pending");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains(payoutId.ToString(), await preview.Content.ReadAsStringAsync());
        Assert.Equal(PayoutStatus.Pending, await PayoutStatusAsync(payoutId));

        var export = await admin.PostAsync("/api/admin/payouts/export", null);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("text/csv", export.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".csv", export.Content.Headers.ContentDisposition?.FileNameStar ?? export.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var csv = await export.Content.ReadAsStringAsync();
        var lines = csv.TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("PayoutId,BookingId,Reason", lines[0]);
        var row = Assert.Single(lines, line => line.Contains(payoutId.ToString()));
        Assert.Contains("\"Completed\"", row);
        Assert.Contains("\"Ananya Desai\"", row);
        Assert.Contains("\"98765 43210\"", row);
        Assert.Contains("\"Exported\"", row);
        Assert.Equal(PayoutStatus.Exported, await PayoutStatusAsync(payoutId));

        var empty = await admin.PostAsync("/api/admin/payouts/export", null);
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);

        var exported = await admin.GetStringAsync("/api/admin/payouts/csv?status=Exported");
        Assert.Contains(payoutId.ToString(), exported);

        var paid = await PostAsync<AdminPayoutBody>(admin, $"/api/admin/payouts/{payoutId}/paid", new { });
        Assert.Equal("Paid", paid.Status);
        Assert.Equal("Completed", paid.BookingStatus);
    }

    [Fact]
    public async Task Browse_filters_instructors_by_city()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var pune = await PostAsync<AreaBody>(admin, "/api/admin/areas", new { name = "Baner", city = "Pune" });

        var client = await SignUpAsync("Rohan Kale", "Male");
        var registered = await PostAsync<RegisterBody>(client, "/api/providers/register", new
        {
            displayName = "Rohan Kale",
            age = 30,
            areaId = pune.Id,
            offersOnline = true,
            onlineRate = 500m,
            googleMeetLink = "https://meet.google.com/abc-defg-hij"
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.Token);
        var me = await client.GetFromJsonAsync<ProviderMeBody>("/api/providers/me", Json);
        var verified = await admin.PostAsJsonAsync($"/api/admin/providers/{me!.Id}/verify", new { });
        Assert.True(verified.IsSuccessStatusCode, await verified.Content.ReadAsStringAsync());

        var anon = _factory.CreateClient();
        var inPune = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?city=pune", Json);
        Assert.Contains(inPune!, p => p.Id == me.Id && p.City == "Pune" && p.Area == "Baner");
        Assert.DoesNotContain(inPune!, p => p.Id == SeedIds.AnanyaProviderId);

        var inMumbai = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?city=Mumbai&area=Bandra", Json);
        Assert.Contains(inMumbai!, p => p.Id == SeedIds.AnanyaProviderId);
        Assert.DoesNotContain(inMumbai!, p => p.Id == me.Id);
    }

    private async Task SetCancelWindowAsync(HttpClient admin, int hours)
    {
        var response = await admin.PatchAsJsonAsync("/api/admin/policy", new { cancelFreeWindowHours = hours });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> TomorrowSlotAsync(HttpClient instructor, string mode, string start)
    {
        var day = MumbaiClock.Today().AddDays(1);
        var end = TimeOnly.Parse(start).AddMinutes(30).ToString("HH:mm");
        var created = await PostAsync<List<SlotBody>>(instructor, "/api/providers/me/slots", new
        {
            mode,
            slots = new[] { new { date = day, start, end } }
        });
        return Assert.Single(created).Id;
    }

    private static async Task<BookingBody> BookAsync(HttpClient client, Guid slotId, string paymentId)
    {
        var order = await PostAsync<CheckoutBody>(client, "/api/bookings/orders", new { slotId });
        return await PostAsync<BookingBody>(client, "/api/bookings/confirm", new
        {
            orderId = order.OrderId,
            paymentId,
            signature = Sign($"{order.OrderId}|{paymentId}")
        });
    }

    private async Task<HttpClient> SignUpAsync(string name, string gender)
    {
        var client = _factory.CreateClient();
        var phone = "+9195" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender, isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<HttpClient> SignInAsync(string phone)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private IReadOnlyList<RecordedRefund> RefundsFor(string paymentId) =>
        Assert.IsType<FakeRazorpayClient>(_factory.Services.GetRequiredService<IRazorpayClient>())
            .Refunds.Where(r => r.PaymentId == paymentId).ToArray();

    private Task<int> PayoutCountAsync(Guid bookingId) =>
        QueryAsync(db => db.PayoutsPending.CountAsync(p => p.BookingId == bookingId));

    private Task<Guid> PayoutIdAsync(Guid bookingId) =>
        QueryAsync(db => db.PayoutsPending.Where(p => p.BookingId == bookingId).Select(p => p.Id).SingleAsync());

    private Task<PayoutStatus> PayoutStatusAsync(Guid payoutId) =>
        QueryAsync(db => db.PayoutsPending.Where(p => p.Id == payoutId).Select(p => p.Status).SingleAsync());

    private async Task<T> QueryAsync<T>(Func<YogaDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<YogaDbContext>());
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(KeySecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record AuthBody(string Token);
    private sealed record RegisterBody(string Token);
    private sealed record ProviderMeBody(Guid Id);
    private sealed record ProviderBody(Guid Id, string DisplayName, string Area, string City);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record SlotBody(Guid Id);
    private sealed record CheckoutBody(string OrderId);
    private sealed record QuoteBody(decimal Amount, decimal LateCancelFee, decimal Refund, string Currency, decimal LateCancelFeePercent, DateTimeOffset? FreeUntil);
    private sealed record BookingBody(
        Guid Id,
        string Status,
        decimal Amount,
        string PaymentStatus,
        string? GatewayPaymentId,
        decimal RefundedAmount,
        decimal? LateCancelFee,
        string? CancelledBy,
        string? CancelReason);
    private sealed record AdminBookingBody(
        Guid Id,
        string Status,
        string? PaymentStatus,
        decimal? RefundedAmount,
        decimal? LateCancelFee,
        string? CancelledBy,
        string? CancelReason);
    private sealed record InstructorPayoutBody(Guid BookingId, decimal GrossAmount, decimal FeeAmount, decimal NetAmount, string BookingStatus);
    private sealed record AdminPayoutBody(Guid Id, string Status, string BookingStatus);
}

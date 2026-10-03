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

public class PlatformSettingsTests : IClassFixture<YogaApiFactory>
{
    private const string KeySecret = "dev-only-not-a-live-key-secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public PlatformSettingsTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Settings_changes_apply_to_new_bookings_only()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        try
        {
            await AdminSettingsClient.PatchAsync(admin, version => new
            {
                version,
                commissionPercent = 15m,
                convenienceFee = 0m,
                cancelFreeWindowHours = 0,
                lateCancelFeeType = "Percent",
                lateCancelFeeValue = 50m
            });
            var oldCustomer = await SignUpAsync("Old Terms Customer");
            var oldBooking = await BookAsync(oldCustomer, await TomorrowSlotAsync(instructor, "05:05"), "pay_old_terms");
            await PostAsync<BookingBody>(instructor, $"/api/bookings/{oldBooking.Id}/accept", new { });
            Assert.Equal(0m, oldBooking.ConvenienceFee);

            await AdminSettingsClient.PatchAsync(admin, version => new
            {
                version,
                commissionPercent = 20m,
                convenienceFee = 30m,
                cancelFreeWindowHours = 48,
                lateCancelFeeType = "Flat",
                lateCancelFeeValue = 100m
            });
            var newCustomer = await SignUpAsync("New Terms Customer");
            var newSlot = await TomorrowSlotAsync(instructor, "05:45");
            var order = await PostAsync<CheckoutBody>(newCustomer, "/api/bookings/orders", new { slotId = newSlot });
            Assert.Equal(749m, order.SessionAmount);
            Assert.Equal(30m, order.ConvenienceFee);
            Assert.Equal(779m, order.Amount);
            var newBooking = await ConfirmAsync(newCustomer, order.OrderId, "pay_new_terms");
            await PostAsync<BookingBody>(instructor, $"/api/bookings/{newBooking.Id}/accept", new { });
            Assert.Equal(749m, newBooking.Amount);
            Assert.Equal(30m, newBooking.ConvenienceFee);
            Assert.Equal(779m, await QueryAsync(db => db.Payments.Where(p => p.BookingId == newBooking.Id).Select(p => p.Amount).SingleAsync()));

            // The old booking keeps its 0 h window: still a free cancel even though the new window is 48 h.
            var oldQuote = await oldCustomer.GetFromJsonAsync<QuoteBody>($"/api/bookings/{oldBooking.Id}/cancel-quote", Json);
            Assert.Equal(0m, oldQuote!.LateCancelFee);
            Assert.Equal(749m, oldQuote.Amount);
            Assert.Equal(749m, oldQuote.Refund);
            Assert.Equal("Percent", oldQuote.LateCancelFeeType);
            Assert.Equal(50m, oldQuote.LateCancelFeeValue);

            var newQuote = await newCustomer.GetFromJsonAsync<QuoteBody>($"/api/bookings/{newBooking.Id}/cancel-quote", Json);
            Assert.Equal(779m, newQuote!.Amount);
            Assert.Equal(749m, newQuote.SessionAmount);
            Assert.Equal(100m, newQuote.LateCancelFee);
            Assert.Equal(30m, newQuote.ConvenienceFeeKept);
            Assert.Equal(649m, newQuote.Refund);
            Assert.Equal("Flat", newQuote.LateCancelFeeType);

            // Payouts use each booking's own commission.
            await PostAsync<BookingBody>(instructor, $"/api/bookings/{oldBooking.Id}/complete", new { });
            var cancelled = await PostAsync<BookingBody>(newCustomer, $"/api/bookings/{newBooking.Id}/cancel", new { });
            Assert.Equal(649m, cancelled.RefundedAmount);
            Assert.Equal(64900, Assert.Single(RefundsFor(newBooking.GatewayPaymentId!)).AmountPaise);

            var payouts = await instructor.GetFromJsonAsync<List<PayoutBody>>("/api/providers/me/payouts", Json);
            var oldPayout = Assert.Single(payouts!, p => p.BookingId == oldBooking.Id);
            Assert.Equal(749m, oldPayout.GrossAmount);
            Assert.Equal(112.35m, oldPayout.FeeAmount);
            Assert.Equal(636.65m, oldPayout.NetAmount);
            var newPayout = Assert.Single(payouts!, p => p.BookingId == newBooking.Id);
            Assert.Equal(100m, newPayout.GrossAmount);
            Assert.Equal(20m, newPayout.FeeAmount);
            Assert.Equal(80m, newPayout.NetAmount);
        }
        finally
        {
            await AdminSettingsClient.ResetFeesAsync(admin);
        }
    }

    [Fact]
    public async Task Two_admins_saving_the_same_version_get_one_save_and_one_conflict()
    {
        var first = await SignInAsync(SeedIds.AdminPhone);
        var second = await SignInAsync(SeedIds.AdminPhone);
        var before = await AdminSettingsClient.GetAsync(first);
        try
        {
            var responses = await Task.WhenAll(
                first.PatchAsJsonAsync("/api/admin/settings", new { version = before.Version, commissionPercent = 18m }),
                second.PatchAsJsonAsync("/api/admin/settings", new { version = before.Version, commissionPercent = 22m }));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("Another admin saved", (await conflict.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error, StringComparison.Ordinal);

            var after = await AdminSettingsClient.GetAsync(first);
            Assert.Equal(before.Version + 1, after.Version);
            Assert.Contains(after.CommissionPercent, new[] { 18m, 22m });

            var audit = await first.GetFromJsonAsync<List<AuditBody>>("/api/admin/settings/audit", Json);
            var row = Assert.Single(audit!, a => a.Field == "CommissionPercent" && a.NewValue == Money(after.CommissionPercent));
            Assert.Equal(Money(before.CommissionPercent), row.OldValue);
            Assert.Equal(SeedIds.AdminUserId, row.AdminUserId);

            var stale = await first.PatchAsJsonAsync("/api/admin/settings", new { version = before.Version, commissionPercent = 10m });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var noVersion = await first.PatchAsJsonAsync("/api/admin/settings", new { commissionPercent = 10m });
            Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
            var invalid = await first.PatchAsJsonAsync("/api/admin/settings", new { version = after.Version, commissionPercent = 120m });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(after.Version, (await AdminSettingsClient.GetAsync(first)).Version);
        }
        finally
        {
            await AdminSettingsClient.ResetFeesAsync(first);
        }
    }

    [Fact]
    public async Task Banner_text_comes_from_settings_and_refreshes_on_save()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var anon = _factory.CreateClient();
        Assert.Equal(new BannerBody(null, null, null), await anon.GetFromJsonAsync<BannerBody>("/api/site/banner", Json));

        await AdminSettingsClient.PatchAsync(admin, version => new
        {
            version,
            bannerTitle = "  Find your calm  ",
            bannerSubtitle = "Verified instructors near you",
            bannerOffer = "First class 20% off"
        });
        Assert.Equal(
            new BannerBody("Find your calm", "Verified instructors near you", "First class 20% off"),
            await anon.GetFromJsonAsync<BannerBody>("/api/site/banner", Json));

        await AdminSettingsClient.PatchAsync(admin, version => new { version, bannerTitle = "", bannerSubtitle = " ", bannerOffer = "" });
        Assert.Equal(new BannerBody(null, null, null), await anon.GetFromJsonAsync<BannerBody>("/api/site/banner", Json));

        var tooLong = await admin.PatchAsJsonAsync("/api/admin/settings", new
        {
            version = (await AdminSettingsClient.GetAsync(admin)).Version,
            bannerTitle = new string('a', PlatformSettingsRules.BannerTitleMax + 1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Inactive_area_is_hidden_from_browse_but_its_bookings_carry_on()
    {
        var admin = await SignInAsync(SeedIds.AdminPhone);
        var instructor = await SignInAsync(SeedIds.AnanyaPhone);
        var customer = await SignUpAsync("Area Closing Customer");
        var booked = await BookAsync(customer, await TomorrowSlotAsync(instructor, "06:25"), "pay_area_closed");
        var bandra = await QueryAsync(db => db.Areas.SingleAsync(a => a.Id == SeedIds.AreaBandra));
        var anon = _factory.CreateClient();
        Assert.Contains(await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json) ?? [], p => p.Id == SeedIds.AnanyaProviderId);

        await PatchAsync(admin, $"/api/admin/areas/{bandra.Id}", new { isActive = false });
        try
        {
            var areas = await anon.GetFromJsonAsync<List<AreaBody>>("/api/areas", Json);
            Assert.DoesNotContain(areas!, a => a.Id == bandra.Id);
            var browse = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json);
            Assert.DoesNotContain(browse!, p => p.Id == SeedIds.AnanyaProviderId);
            var filtered = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers?area=Bandra", Json);
            Assert.Empty(filtered!);

            var mine = await customer.GetFromJsonAsync<List<BookingBody>>("/api/bookings/me", Json);
            Assert.Contains(mine!, b => b.Id == booked.Id && b.Status == "PendingAccept");
            var accepted = await PostAsync<BookingBody>(instructor, $"/api/bookings/{booked.Id}/accept", new { });
            Assert.Equal("Upcoming", accepted.Status);
        }
        finally
        {
            await PatchAsync(admin, $"/api/admin/areas/{bandra.Id}", new { isActive = true });
        }

        var reopened = await anon.GetFromJsonAsync<List<ProviderBody>>("/api/providers", Json);
        Assert.Contains(reopened!, p => p.Id == SeedIds.AnanyaProviderId);
    }

    private static async Task<Guid> TomorrowSlotAsync(HttpClient instructor, string start)
    {
        var day = MumbaiClock.Today().AddDays(1);
        var end = TimeOnly.Parse(start).AddMinutes(30).ToString("HH:mm");
        var created = await PostAsync<List<SlotBody>>(instructor, "/api/providers/me/slots", new
        {
            mode = "Studio",
            slots = new[] { new { date = day, start, end } }
        });
        return Assert.Single(created).Id;
    }

    private static async Task<BookingBody> BookAsync(HttpClient client, Guid slotId, string paymentId)
    {
        var order = await PostAsync<CheckoutBody>(client, "/api/bookings/orders", new { slotId });
        return await ConfirmAsync(client, order.OrderId, paymentId);
    }

    private static Task<BookingBody> ConfirmAsync(HttpClient client, string orderId, string paymentId) =>
        PostAsync<BookingBody>(client, "/api/bookings/confirm", new
        {
            orderId,
            paymentId,
            signature = Sign($"{orderId}|{paymentId}")
        });

    private async Task<HttpClient> SignUpAsync(string name)
    {
        var client = _factory.CreateClient();
        var phone = "+9193" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name, gender = "Female", isNewUser = true });
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

    private static async Task PatchAsync(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string Money(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(KeySecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private sealed record ErrorBody(string Error);
    private sealed record OtpBody(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);
    private sealed record AuthBody(string Token);
    private sealed record SlotBody(Guid Id);
    private sealed record AreaBody(Guid Id, string City, string Name);
    private sealed record ProviderBody(Guid Id, string DisplayName);
    private sealed record CheckoutBody(string OrderId, decimal Amount, decimal SessionAmount, decimal ConvenienceFee);
    private sealed record BookingBody(
        Guid Id,
        string Status,
        decimal Amount,
        decimal ConvenienceFee,
        string? GatewayPaymentId,
        decimal RefundedAmount);
    private sealed record QuoteBody(
        decimal Amount,
        decimal SessionAmount,
        decimal ConvenienceFee,
        decimal LateCancelFee,
        decimal ConvenienceFeeKept,
        decimal Refund,
        string LateCancelFeeType,
        decimal LateCancelFeeValue);
    private sealed record PayoutBody(Guid BookingId, decimal GrossAmount, decimal FeeAmount, decimal NetAmount);
    private sealed record AuditBody(Guid AdminUserId, string Field, string? OldValue, string? NewValue);
    private sealed record BannerBody(string? Title, string? Subtitle, string? Offer);
}

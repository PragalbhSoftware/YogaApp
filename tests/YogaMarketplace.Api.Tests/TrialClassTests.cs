using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class TrialClassTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public TrialClassTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Free_trial_charges_only_the_convenience_fee_once_per_customer_and_a_customer_cancel_keeps_it_used()
    {
        var (admin, instructor) = await StaffAsync();
        await AdminSettingsClient.PatchAsync(admin, version => new { version, convenienceFee = 25m });
        try
        {
            var trial = await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 0m });
            Assert.True(trial.Enabled);
            Assert.Equal(0m, trial.Price);

            var detail = await _factory.CreateClient().GetFromJsonAsync<DetailBody>($"/api/providers/{SeedIds.AnanyaProviderId}", Json);
            Assert.Equal(0m, detail!.Trial!.Price);

            var customer = await SignUpAsync();
            Assert.Equal(new StatusBody(true, 0m, false), await TrialStatusAsync(customer));

            var slot = await TomorrowSlotAsync(instructor, "01:00");
            var quote = await customer.GetFromJsonAsync<QuoteBody>($"/api/bookings/quote?slotId={slot}&trial=true", Json);
            Assert.Equal(new QuoteBody(0m, 25m, 25m, true), quote);

            var booking = await BookAsync(customer, slot, trial: true);
            Assert.True(booking.IsTrial);
            Assert.Equal(0m, booking.Amount);
            Assert.Equal(25m, await PaidAsync(booking.Id));
            Assert.True((await TrialStatusAsync(customer)).Used);

            var second = await TomorrowSlotAsync(instructor, "01:40");
            await AssertConflictAsync(
                await customer.GetAsync($"/api/bookings/quote?slotId={second}&trial=true"), TrialRules.AlreadyUsedMessage);
            await AssertConflictAsync(
                await customer.PostAsJsonAsync("/api/bookings/orders", new { slotId = second, trial = true }), TrialRules.AlreadyUsedMessage);

            var regular = await customer.GetFromJsonAsync<QuoteBody>($"/api/bookings/quote?slotId={second}", Json);
            Assert.Equal(new QuoteBody(749m, 25m, 774m, false), regular);

            var cancel = await customer.PostAsJsonAsync($"/api/bookings/{booking.Id}/cancel", new { });
            Assert.True(cancel.IsSuccessStatusCode, await cancel.Content.ReadAsStringAsync());
            Assert.True((await TrialStatusAsync(customer)).Used);
            Assert.True(await QueryAsync(db => db.Bookings.Where(b => b.Id == booking.Id).Select(b => b.TrialConsumed).SingleAsync()));
            await AssertConflictAsync(
                await customer.PostAsJsonAsync("/api/bookings/orders", new { slotId = second, trial = true }), TrialRules.AlreadyUsedMessage);
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
            await AdminSettingsClient.ResetFeesAsync(admin);
        }
    }

    [Fact]
    public async Task Instructor_decline_or_admin_cancel_gives_the_trial_back()
    {
        var (admin, instructor) = await StaffAsync();
        await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 199m });
        try
        {
            var customer = await SignUpAsync();
            var declined = await BookAsync(customer, await TomorrowSlotAsync(instructor, "02:20"), trial: true);
            Assert.Equal(199m, declined.Amount);
            Assert.True((await TrialStatusAsync(customer)).Used);

            var decline = await instructor.PostAsJsonAsync($"/api/bookings/{declined.Id}/decline", new { });
            Assert.True(decline.IsSuccessStatusCode, await decline.Content.ReadAsStringAsync());
            Assert.False((await TrialStatusAsync(customer)).Used);

            var rebookedAfterDecline = await BookAsync(customer, await TomorrowSlotAsync(instructor, "03:00"), trial: true);
            Assert.True(rebookedAfterDecline.IsTrial);
            var adminCancel = await admin.PostAsJsonAsync(
                $"/api/admin/bookings/{rebookedAfterDecline.Id}/cancel", new { reason = "Instructor unwell" });
            Assert.True(adminCancel.IsSuccessStatusCode, await adminCancel.Content.ReadAsStringAsync());
            Assert.False((await TrialStatusAsync(customer)).Used);

            var rebookedAfterAdminCancel = await BookAsync(customer, await TomorrowSlotAsync(instructor, "06:20"), trial: true);
            Assert.True(rebookedAfterAdminCancel.IsTrial);
            Assert.Equal(
                [rebookedAfterAdminCancel.Id],
                await QueryAsync(db => db.Bookings
                    .Where(b => b.ProviderId == SeedIds.AnanyaProviderId && b.TrialConsumed
                        && (b.Id == declined.Id || b.Id == rebookedAfterDecline.Id || b.Id == rebookedAfterAdminCancel.Id))
                    .Select(b => b.Id)
                    .ToListAsync()));
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    [Fact]
    public async Task A_second_trial_paid_in_another_tab_is_refunded_at_capture()
    {
        var (_, instructor) = await StaffAsync();
        await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 99m });
        try
        {
            var (customer, phone) = await SignUpWithPhoneAsync();
            var first = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "03:40"), trial = true });
            var second = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "04:20"), trial = true });

            await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = first.OrderId });

            var duplicate = await customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = second.OrderId });
            await AssertConflictAsync(duplicate, "refunded");
            Assert.Equal(CheckoutStatus.Refunded, await QueryAsync(db =>
                db.CheckoutIntents.Where(c => c.GatewayOrderId == second.OrderId).Select(c => c.Status).SingleAsync()));

            var replay = await customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = second.OrderId });
            await AssertConflictAsync(replay, "refunded");
            Assert.Equal(1, await QueryAsync(db => db.Bookings.CountAsync(b => b.IsTrial && b.Customer!.Phone == phone)));
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    [Fact]
    public async Task Double_clicked_trial_payments_book_once_and_refund_the_other()
    {
        var (_, instructor) = await StaffAsync();
        await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 99m });
        try
        {
            var (customer, phone) = await SignUpWithPhoneAsync();
            var first = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "07:00"), trial = true });
            var second = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "07:40"), trial = true });

            var responses = await Task.WhenAll(
                customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = first.OrderId }),
                customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = second.OrderId }));

            Assert.Single(responses, r => r.IsSuccessStatusCode);
            await AssertConflictAsync(Assert.Single(responses, r => !r.IsSuccessStatusCode), "refunded");
            Assert.Equal(1, await QueryAsync(db => db.Bookings.CountAsync(b => b.TrialConsumed && b.Customer!.Phone == phone)));
            Assert.Equal(1, await QueryAsync(db => db.CheckoutIntents.CountAsync(c =>
                (c.GatewayOrderId == first.OrderId || c.GatewayOrderId == second.OrderId) && c.Status == CheckoutStatus.Refunded)));
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    [Fact]
    public async Task A_second_trial_that_slips_past_the_check_hits_the_unique_index_and_is_refunded()
    {
        var staleCheck = new StaleTrialCheckInterceptor();
        using var racing = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(staleCheck)));
        var instructor = await SignInAsync(racing, SeedIds.AnanyaPhone);
        await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 99m });
        try
        {
            var (customer, phone) = await SignUpWithPhoneAsync(racing);
            var first = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "08:20"), trial = true });
            var second = await PostAsync<OrderBody>(customer, "/api/bookings/orders",
                new { slotId = await TomorrowSlotAsync(instructor, "09:00"), trial = true });
            await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = first.OrderId });

            staleCheck.Arm();
            var duplicate = await customer.PostAsJsonAsync("/api/bookings/local-confirm", new { orderId = second.OrderId });

            Assert.True(staleCheck.Used);
            await AssertConflictAsync(duplicate, "refunded");
            Assert.Equal(CheckoutStatus.Refunded, await QueryAsync(db =>
                db.CheckoutIntents.Where(c => c.GatewayOrderId == second.OrderId).Select(c => c.Status).SingleAsync()));
            Assert.Equal(1, await QueryAsync(db => db.Bookings.CountAsync(b => b.IsTrial && b.Customer!.Phone == phone)));
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
        }
    }

    [Fact]
    public async Task Trial_price_rules_and_a_free_trial_needs_a_convenience_fee()
    {
        var (admin, instructor) = await StaffAsync();
        var free = await instructor.PutAsJsonAsync("/api/providers/me/trial", new { enabled = true, price = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, free.StatusCode);
        Assert.Contains("at least ₹1", await free.Content.ReadAsStringAsync());

        var aboveRegular = await instructor.PutAsJsonAsync("/api/providers/me/trial", new { enabled = true, price = 1000m });
        Assert.Equal(HttpStatusCode.BadRequest, aboveRegular.StatusCode);

        var customer = await SignUpAsync();
        var slot = await TomorrowSlotAsync(instructor, "05:00");
        await AssertConflictAsync(
            await customer.GetAsync($"/api/bookings/quote?slotId={slot}&trial=true"), TrialRules.NotOfferedMessage);

        await AdminSettingsClient.PatchAsync(admin, version => new { version, convenienceFee = 25m });
        try
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = true, price = 0m });
            await AdminSettingsClient.PatchAsync(admin, version => new { version, convenienceFee = 0m });
            await AssertConflictAsync(
                await customer.GetAsync($"/api/bookings/quote?slotId={slot}&trial=true"), TrialRules.BelowMinimumMessage);
        }
        finally
        {
            await PutAsync<TrialBody>(instructor, "/api/providers/me/trial", new { enabled = false });
            await AdminSettingsClient.ResetFeesAsync(admin);
        }
    }

    private async Task<(HttpClient Admin, HttpClient Instructor)> StaffAsync() =>
        (await SignInAsync(SeedIds.AdminPhone), await SignInAsync(SeedIds.AnanyaPhone));

    private static async Task<StatusBody> TrialStatusAsync(HttpClient customer) =>
        (await customer.GetFromJsonAsync<StatusBody>($"/api/bookings/trial?providerId={SeedIds.AnanyaProviderId}", Json))!;

    private Task<decimal> PaidAsync(Guid bookingId) =>
        QueryAsync(db => db.Payments.Where(p => p.BookingId == bookingId).Select(p => p.Amount).SingleAsync());

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

    private static async Task<BookingBody> BookAsync(HttpClient customer, Guid slotId, bool trial)
    {
        var order = await PostAsync<OrderBody>(customer, "/api/bookings/orders", new { slotId, trial });
        return await PostAsync<BookingBody>(customer, "/api/bookings/local-confirm", new { orderId = order.OrderId });
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response, string message)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, payload);
        Assert.Contains(message, payload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> SignUpAsync() => (await SignUpWithPhoneAsync()).Client;

    private Task<(HttpClient Client, string Phone)> SignUpWithPhoneAsync() => SignUpWithPhoneAsync(_factory);

    private static async Task<(HttpClient Client, string Phone)> SignUpWithPhoneAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var phone = "+9192" + Random.Shared.Next(10000000, 99999999);
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Kavya Iyer", gender = "Female", isNewUser = true });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, phone);
    }

    private Task<HttpClient> SignInAsync(string phone) => SignInAsync(_factory, phone);

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> factory, string phone)
    {
        var client = factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, isNewUser = false });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

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

    private static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record OtpBody(string DevCode);
    private sealed record AuthBody(string Token);
    private sealed record SlotBody(Guid Id);
    private sealed record OrderBody(string OrderId);
    private sealed record BookingBody(Guid Id, decimal Amount, bool IsTrial);
    private sealed record TrialBody(bool Enabled, decimal? Price);
    private sealed record StatusBody(bool Offered, decimal? Price, bool Used);
    private sealed record QuoteBody(decimal SessionAmount, decimal ConvenienceFee, decimal Total, bool IsTrial);
    private sealed record TrialOfferBody(Guid ServiceId, decimal Price);
    private sealed record DetailBody(TrialOfferBody? Trial);
}

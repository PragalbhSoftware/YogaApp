using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Tests;

namespace YogaMarketplace.Smoke;

/// <summary>
/// Post-deploy smoke. With SMOKE_BASE_URL set it runs against that site, signing in with the demo phone
/// SMOKE_PHONE and code SMOKE_OTP_CODE. Without it, it runs against the in-memory test host.
/// A real Razorpay Test checkout needs a browser, so against a real gateway it stops after creating the
/// Test order; with the fake gateway it also pays, cancels and checks the refund.
/// </summary>
public class SmokeTests
{
    private const string InMemoryPhone = "+917000077001";
    private const string InMemoryCode = "135790";
    private static readonly string[] BookableModes = ["Online", "Studio"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITestOutputHelper _log;

    public SmokeTests(ITestOutputHelper log)
    {
        _log = log;
    }

    [Fact]
    public async Task Sign_in_book_pay_cancel_and_refund()
    {
        var baseUrl = Environment.GetEnvironmentVariable("SMOKE_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            using var factory = new YogaApiFactory().WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Otp:DemoPhones"] = InMemoryPhone,
                    ["Otp:DemoCode"] = InMemoryCode
                })));
            var paid = await RunAsync(factory.CreateClient(), InMemoryPhone, InMemoryCode);
            Assert.True(paid, "The in-memory host uses the fake gateway, so the full pay, cancel and refund path must run.");
            return;
        }

        using var client = new HttpClient { BaseAddress = new Uri(baseUrl.Trim().TrimEnd('/') + "/") };
        await RunAsync(client, Required("SMOKE_PHONE"), Required("SMOKE_OTP_CODE"));
    }

    /// <summary>True when the payment, cancel and refund steps ran (fake gateway only).</summary>
    private async Task<bool> RunAsync(HttpClient client, string phone, string code)
    {
        var ready = await client.GetAsync("health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        _log.WriteLine("Health: ready.");

        var token = await SignInAsync(client, phone, code);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _log.WriteLine("Signed in with the demo phone.");

        var slot = await FindOpenSlotAsync(client);
        _log.WriteLine($"Slot {slot.Id}: {slot.Mode} on {slot.Date} at {slot.Start}.");

        var order = await PostAsync<OrderBody>(client, "api/bookings/orders", new { slotId = slot.Id });
        Assert.StartsWith("order_", order.OrderId, StringComparison.Ordinal);
        Assert.True(order.AmountPaise > 0);
        if (!order.LocalCapture)
        {
            Assert.StartsWith(ProductionSafety.RazorpayTestKeyPrefix, order.KeyId, StringComparison.Ordinal);
            _log.WriteLine($"Razorpay Test order {order.OrderId} created. Paying needs the browser checkout, so the smoke stops here.");
            return false;
        }

        var booking = await PostAsync<BookingBody>(client, "api/bookings/local-confirm", new { orderId = order.OrderId });
        Assert.Equal("Paid", booking.PaymentStatus);
        _log.WriteLine($"Booked and paid: {booking.Id} ({booking.Status}).");

        var quote = await client.GetFromJsonAsync<CancelQuoteBody>($"api/bookings/{booking.Id}/cancel-quote", Json);
        var cancelled = await PostAsync<BookingBody>(client, $"api/bookings/{booking.Id}/cancel", new { });
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(quote!.Refund, cancelled.RefundedAmount);
        if (quote.Refund > 0)
            Assert.Contains(cancelled.PaymentStatus, new[] { "Refunded", "PartiallyRefunded" });
        _log.WriteLine($"Cancelled. Refunded {cancelled.RefundedAmount} ({cancelled.PaymentStatus}).");
        return true;
    }

    private static async Task<string> SignInAsync(HttpClient client, string phone, string code)
    {
        var existing = await client.PostAsJsonAsync("api/auth/otp/request", new { phone, isNewUser = false });
        if (existing.StatusCode == HttpStatusCode.NotFound)
            await PostAsync<JsonElement>(client, "api/auth/otp/request", new { phone, name = "Smoke Test", gender = "Other", isNewUser = true });
        else
            await EnsureSuccessAsync(existing);

        var auth = await PostAsync<AuthBody>(client, "api/auth/otp/verify", new { phone, code });
        return auth.Token;
    }

    /// <summary>The furthest-out open slot, so the cancel stays clear of any late-cancel window the admin sets.</summary>
    private static async Task<SlotBody> FindOpenSlotAsync(HttpClient client)
    {
        var providers = await client.GetFromJsonAsync<List<ProviderBody>>("api/providers", Json);
        var slots = new List<SlotBody>();
        foreach (var provider in providers!)
        {
            foreach (var mode in provider.Modes.Select(m => m.Mode).Intersect(BookableModes))
            {
                var list = await client.GetFromJsonAsync<SlotListBody>($"api/providers/{provider.Id}/slots?mode={mode}", Json);
                slots.AddRange(list!.Slots);
            }
        }

        Assert.True(slots.Count > 0, "No open Online or Studio slot. Load the demo data or add slots first.");
        return slots.OrderByDescending(s => s.Date).ThenByDescending(s => s.Start).First();
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when SMOKE_BASE_URL is set.");

    private sealed record AuthBody(string Token);
    private sealed record ModeBody(string Mode);
    private sealed record ProviderBody(Guid Id, List<ModeBody> Modes);
    private sealed record SlotBody(Guid Id, string Mode, DateOnly Date, string Start);
    private sealed record SlotListBody(List<SlotBody> Slots);
    private sealed record OrderBody(string KeyId, string OrderId, long AmountPaise, bool LocalCapture);
    private sealed record CancelQuoteBody(decimal Refund);
    private sealed record BookingBody(Guid Id, string Status, string PaymentStatus, decimal RefundedAmount);
}

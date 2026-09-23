using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

public sealed record RazorpayCreatedOrder(string OrderId, long AmountPaise, string Currency);

public sealed record RazorpayPaymentSnapshot(string PaymentId, string OrderId, string Status, long AmountPaise, string Currency);

public interface IRazorpayGateway
{
    Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken);

    bool VerifyCheckoutSignature(string orderId, string paymentId, string signature);

    bool VerifyWebhookSignature(string rawBody, string? signature);

    Task<RazorpayPaymentSnapshot?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken);
}

public static class RazorpaySignatures
{
    public static bool MatchesPayment(string secret, string orderId, string paymentId, string? signature) =>
        Matches(secret, $"{orderId}|{paymentId}", signature);

    public static bool MatchesWebhook(string secret, string rawBody, string? signature) =>
        Matches(secret, rawBody, signature);

    public static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static bool Matches(string secret, string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Sign(secret, payload));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

public static class RazorpayMoney
{
    public static long ToPaise(decimal rupees)
    {
        var paise = decimal.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero);
        if (paise < 100 || paise != rupees * 100m)
            throw new DomainException("Amount must be a whole number of paise, at least ₹1.");
        return (long)paise;
    }
}

/// <summary>
/// Local stand-in used when Razorpay:UseFakeGateway is true. Still verifies HMAC signatures.
/// </summary>
public sealed class FakeRazorpayGateway : IRazorpayGateway
{
    private readonly RazorpayOptions _options;

    public FakeRazorpayGateway(IOptions<RazorpayOptions> options)
    {
        _options = options.Value;
    }

    public Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken)
    {
        var orderId = "order_fake_" + Guid.NewGuid().ToString("N");
        return Task.FromResult(new RazorpayCreatedOrder(orderId, amountPaise, currency));
    }

    public bool VerifyCheckoutSignature(string orderId, string paymentId, string signature) =>
        RazorpaySignatures.MatchesPayment(_options.KeySecret, orderId, paymentId, signature);

    public bool VerifyWebhookSignature(string rawBody, string? signature) =>
        RazorpaySignatures.MatchesWebhook(_options.WebhookSecret, rawBody, signature);

    public Task<RazorpayPaymentSnapshot?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken) =>
        Task.FromResult<RazorpayPaymentSnapshot?>(null);
}

public sealed class RazorpayHttpGateway : IRazorpayGateway
{
    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RazorpayHttpGateway> _logger;

    public RazorpayHttpGateway(HttpClient http, IOptions<RazorpayOptions> options, ILogger<RazorpayHttpGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_options.KeyId) && !string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.KeyId}:{_options.KeySecret}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public async Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await _http.PostAsJsonAsync("v1/orders", new
        {
            amount = amountPaise,
            currency,
            receipt,
            notes
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("create order", response, cancellationToken);
            throw new DomainException("Could not start the payment. Try again.", 502);
        }

        var order = await response.Content.ReadFromJsonAsync<OrderWire>(cancellationToken: cancellationToken);
        if (order is null || string.IsNullOrWhiteSpace(order.Id))
            throw new DomainException("Could not start the payment. Try again.", 502);

        return new RazorpayCreatedOrder(order.Id, order.Amount, order.Currency);
    }

    public bool VerifyCheckoutSignature(string orderId, string paymentId, string signature) =>
        RazorpaySignatures.MatchesPayment(_options.KeySecret, orderId, paymentId, signature);

    public bool VerifyWebhookSignature(string rawBody, string? signature) =>
        RazorpaySignatures.MatchesWebhook(_options.WebhookSecret, rawBody, signature);

    public async Task<RazorpayPaymentSnapshot?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await _http.GetAsync($"v1/payments/{Uri.EscapeDataString(paymentId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("fetch payment", response, cancellationToken);
            throw new DomainException("Could not verify the payment. Try again.", 502);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentWire>(cancellationToken: cancellationToken);
        if (payment is null || string.IsNullOrWhiteSpace(payment.Id) || string.IsNullOrWhiteSpace(payment.OrderId))
            return null;

        return new RazorpayPaymentSnapshot(payment.Id, payment.OrderId, payment.Status ?? "", payment.Amount, payment.Currency ?? "");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) || string.IsNullOrWhiteSpace(_options.KeySecret))
            throw new DomainException("Razorpay is not configured.", 503);
    }

    private async Task LogFailureAsync(string action, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 500)
            body = body[..500];
        _logger.LogWarning("Razorpay {Action} failed with {Status}. {Body}", action, (int)response.StatusCode, body);
    }

    private sealed record OrderWire(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency);

    private sealed record PaymentWire(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string? Currency);
}

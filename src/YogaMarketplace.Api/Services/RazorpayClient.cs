using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

public static class RazorpayWire
{
    public const string CapturedStatus = "captured";
    public const string PaymentCapturedEvent = "payment.captured";
    public const string SignatureHeader = "X-Razorpay-Signature";
}

public sealed record RazorpayCreatedOrder(string OrderId, long AmountPaise, string Currency);

public interface IRazorpayClient
{
    string KeyId { get; }

    Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks the checkout HMAC. The live client also requires Razorpay to report this payment as captured for the order and amount.
    /// </summary>
    Task RequireCapturedCheckoutAsync(
        string orderId,
        string paymentId,
        string signature,
        long expectedAmountPaise,
        string expectedCurrency,
        CancellationToken cancellationToken);

    bool VerifyWebhookSignature(string rawBody, string? signature);

    /// <summary>
    /// Refunds a captured payment in full or in part. The fake client records the call and does not contact Razorpay.
    /// </summary>
    Task RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken);
}

internal readonly record struct RecordedRefund(string PaymentId, long AmountPaise, string Receipt);

internal static class RazorpaySignatures
{
    public static bool MatchesPayment(string secret, string orderId, string paymentId, string? signature) =>
        Matches(secret, $"{orderId}|{paymentId}", signature);

    public static bool MatchesWebhook(string secret, string rawBody, string? signature) =>
        Matches(secret, rawBody, signature);

    private static bool Matches(string secret, string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Sign(secret, payload));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

internal static class RazorpayMoney
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
/// Local stand-in used when Razorpay:UseFakeGateway is true. A valid HMAC is treated as a captured payment. No call is made to Razorpay.
/// </summary>
public sealed class FakeRazorpayClient : IRazorpayClient
{
    private readonly RazorpayOptions _options;
    private readonly ConcurrentQueue<RecordedRefund> _refunds = new();

    public FakeRazorpayClient(IOptions<RazorpayOptions> options)
    {
        _options = options.Value;
    }

    internal IReadOnlyCollection<RecordedRefund> Refunds => _refunds.ToArray();

    public string KeyId => _options.KeyId;

    public Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken)
    {
        EnsureKeyId();
        var orderId = "order_fake_" + Guid.NewGuid().ToString("N");
        return Task.FromResult(new RazorpayCreatedOrder(orderId, amountPaise, currency));
    }

    public Task RequireCapturedCheckoutAsync(
        string orderId,
        string paymentId,
        string signature,
        long expectedAmountPaise,
        string expectedCurrency,
        CancellationToken cancellationToken)
    {
        EnsureKeyId();
        if (expectedAmountPaise <= 0 || string.IsNullOrWhiteSpace(expectedCurrency))
            throw new DomainException("Payment amount does not match the slot price.");
        if (!RazorpaySignatures.MatchesPayment(_options.KeySecret, orderId, paymentId, signature))
            throw new DomainException("Payment signature is invalid.");
        return Task.CompletedTask;
    }

    public bool VerifyWebhookSignature(string rawBody, string? signature) =>
        RazorpaySignatures.MatchesWebhook(_options.WebhookSecret, rawBody, signature);

    public Task RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken)
    {
        EnsureKeyId();
        if (string.IsNullOrWhiteSpace(paymentId) || amountPaise <= 0 || string.IsNullOrWhiteSpace(receipt))
            throw new DomainException("This payment cannot be refunded.");

        _refunds.Enqueue(new RecordedRefund(paymentId, amountPaise, receipt));
        return Task.CompletedTask;
    }

    private void EnsureKeyId()
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId))
            throw new DomainException("Razorpay is not configured.", 503);
    }
}

public sealed class RazorpayHttpClient : IRazorpayClient
{
    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RazorpayHttpClient> _logger;

    public RazorpayHttpClient(HttpClient http, IOptions<RazorpayOptions> options, ILogger<RazorpayHttpClient> logger)
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

    public string KeyId => _options.KeyId;

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

    public async Task RequireCapturedCheckoutAsync(
        string orderId,
        string paymentId,
        string signature,
        long expectedAmountPaise,
        string expectedCurrency,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!RazorpaySignatures.MatchesPayment(_options.KeySecret, orderId, paymentId, signature))
            throw new DomainException("Payment signature is invalid.");

        var remote = await FetchPaymentAsync(paymentId, cancellationToken)
            ?? throw new DomainException("Payment was not found at Razorpay.");
        if (!string.Equals(remote.Status, RazorpayWire.CapturedStatus, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Payment is not captured.");
        if (!string.Equals(remote.OrderId, orderId, StringComparison.Ordinal))
            throw new DomainException("Payment does not match this order.");
        var currency = remote.Currency;
        if (remote.Amount != expectedAmountPaise
            || string.IsNullOrWhiteSpace(currency)
            || !currency.Equals(expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Payment amount does not match the slot price.");
        }
    }

    public bool VerifyWebhookSignature(string rawBody, string? signature) =>
        RazorpaySignatures.MatchesWebhook(_options.WebhookSecret, rawBody, signature);

    public async Task RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(paymentId) || amountPaise <= 0 || string.IsNullOrWhiteSpace(receipt))
            throw new DomainException("This payment cannot be refunded.");

        using var response = await _http.PostAsJsonAsync(
            $"v1/payments/{Uri.EscapeDataString(paymentId)}/refund",
            new { amount = amountPaise, receipt },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("refund payment", response, cancellationToken);
            throw new DomainException("Could not refund the payment. Try again.", 502);
        }
    }

    private async Task<PaymentWire?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"v1/payments/{Uri.EscapeDataString(paymentId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("fetch payment", response, cancellationToken);
            throw new DomainException("Could not verify the payment. Try again.", 502);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentWire>(cancellationToken: cancellationToken);
        if (payment is null || string.IsNullOrWhiteSpace(payment.Id) || string.IsNullOrWhiteSpace(payment.OrderId))
            return null;
        return payment;
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

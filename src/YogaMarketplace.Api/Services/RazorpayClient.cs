using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

public static class RazorpayWire
{
    public const string CapturedStatus = "captured";
    public const string RefundedStatus = "refunded";
    public const string FailedStatus = "failed";
    public const string PaymentCapturedEvent = "payment.captured";
    public const string PaymentFailedEvent = "payment.failed";
    public const string RefundProcessedEvent = "refund.processed";
    public const string RefundFailedEvent = "refund.failed";
    public const string SignatureHeader = "X-Razorpay-Signature";
    public const string EventIdHeader = "X-Razorpay-Event-Id";
}

public sealed record RazorpayCreatedOrder(string OrderId, long AmountPaise, string Currency);

public sealed record RazorpayPaymentRecord(string Id, string? OrderId, string? Status, long AmountPaise, long AmountRefundedPaise, string? Currency);

public sealed record RazorpayRefundRecord(string Id, string PaymentId, long AmountPaise, string? Status, string? Receipt);

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

    bool SupportsLocalCapture { get; }

    (string PaymentId, string Signature) CreateLocalCapture(string orderId);

    bool VerifyWebhookSignature(string rawBody, string? signature);

    /// <summary>
    /// Refunds a captured payment in full or in part and returns Razorpay's refund id, or null if the reply had none.
    /// The fake client records the call and does not contact Razorpay.
    /// </summary>
    Task<string?> RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken);

    /// <summary>Every payment Razorpay created between <paramref name="from"/> and <paramref name="to"/>, all pages.</summary>
    Task<IReadOnlyList<RazorpayPaymentRecord>> ListPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>Every refund Razorpay created between <paramref name="from"/> and <paramref name="to"/>, all pages.</summary>
    Task<IReadOnlyList<RazorpayRefundRecord>> ListRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>One payment, or null when Razorpay has no such payment.</summary>
    Task<RazorpayPaymentRecord?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken);
}

internal readonly record struct RecordedRefund(string PaymentId, long AmountPaise, string Receipt, string RefundId);

internal static class RazorpaySignatures
{
    public static string ForPayment(string secret, string orderId, string paymentId) =>
        Sign(secret, $"{orderId}|{paymentId}");

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

    public static decimal FromPaise(long paise) => paise / 100m;
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

    public bool SupportsLocalCapture => true;

    public (string PaymentId, string Signature) CreateLocalCapture(string orderId)
    {
        EnsureKeyId();
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(_options.KeySecret))
            throw new DomainException("Razorpay is not configured.", 503);
        var paymentId = "pay_fake_" + Guid.NewGuid().ToString("N");
        return (paymentId, RazorpaySignatures.ForPayment(_options.KeySecret, orderId, paymentId));
    }

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

    public Task<string?> RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken)
    {
        EnsureKeyId();
        if (string.IsNullOrWhiteSpace(paymentId) || amountPaise <= 0 || string.IsNullOrWhiteSpace(receipt))
            throw new DomainException("This payment cannot be refunded.");

        var refundId = "rfnd_fake_" + Guid.NewGuid().ToString("N");
        _refunds.Enqueue(new RecordedRefund(paymentId, amountPaise, receipt, refundId));
        return Task.FromResult<string?>(refundId);
    }

    /// <summary>The fake keeps no ledger; reconciliation skips the gateway comparison when this client is in use.</summary>
    public Task<IReadOnlyList<RazorpayPaymentRecord>> ListPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RazorpayPaymentRecord>>([]);

    public Task<IReadOnlyList<RazorpayRefundRecord>> ListRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RazorpayRefundRecord>>([]);

    public Task<RazorpayPaymentRecord?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken) =>
        Task.FromResult<RazorpayPaymentRecord?>(null);

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

    public bool SupportsLocalCapture => false;

    public (string PaymentId, string Signature) CreateLocalCapture(string orderId) =>
        throw new DomainException("Pay with Razorpay Checkout.", 400);

    public async Task<RazorpayCreatedOrder> CreateOrderAsync(
        long amountPaise,
        string currency,
        string receipt,
        IReadOnlyDictionary<string, string> notes,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await SendAsync(
            "create order",
            "Could not start the payment. Try again.",
            () => _http.PostAsJsonAsync("v1/orders", new { amount = amountPaise, currency, receipt, notes }, cancellationToken),
            cancellationToken);

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

        var remote = await FetchPaymentWireAsync(paymentId, cancellationToken)
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

    public async Task<string?> RefundPaymentAsync(string paymentId, long amountPaise, string receipt, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(paymentId) || amountPaise <= 0 || string.IsNullOrWhiteSpace(receipt))
            throw new DomainException("This payment cannot be refunded.");

        using var response = await SendAsync(
            "refund payment",
            "Could not reach Razorpay to refund. Try again.",
            () => _http.PostAsJsonAsync(
                $"v1/payments/{Uri.EscapeDataString(paymentId)}/refund",
                new { amount = amountPaise, receipt },
                cancellationToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("refund payment", response, cancellationToken);
            throw new DomainException("Could not refund the payment. Try again.", 502);
        }

        // The refund went through; a missing or unreadable id must not undo it.
        try
        {
            var refund = await response.Content.ReadFromJsonAsync<RefundWire>(cancellationToken: CancellationToken.None);
            return string.IsNullOrWhiteSpace(refund?.Id) ? null : refund.Id;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Razorpay refund of payment {PaymentId} returned no readable refund id.", paymentId);
            return null;
        }
    }

    public Task<IReadOnlyList<RazorpayPaymentRecord>> ListPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        ListAllAsync<PaymentWire, RazorpayPaymentRecord>("payments", from, to, ToRecord, cancellationToken);

    public Task<IReadOnlyList<RazorpayRefundRecord>> ListRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        ListAllAsync<RefundListWire, RazorpayRefundRecord>(
            "refunds",
            from,
            to,
            r => new RazorpayRefundRecord(r.Id, r.PaymentId, r.Amount, r.Status, r.Receipt),
            cancellationToken);

    public async Task<RazorpayPaymentRecord?> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var payment = await FetchPaymentWireAsync(paymentId, cancellationToken);
        return payment is null ? null : ToRecord(payment);
    }

    private static RazorpayPaymentRecord ToRecord(PaymentWire p) =>
        new(p.Id, p.OrderId, p.Status, p.Amount, p.AmountRefunded, p.Currency);

    /// <summary>Razorpay's largest page. Pages are read until one comes back short.</summary>
    private const int ListPageSize = 100;

    private async Task<IReadOnlyList<TRecord>> ListAllAsync<TWire, TRecord>(
        string resource,
        DateTimeOffset from,
        DateTimeOffset to,
        Func<TWire, TRecord> map,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var action = "list " + resource;
        var all = new List<TRecord>();
        for (var skip = 0; ; skip += ListPageSize)
        {
            var url = $"v1/{resource}?from={from.ToUnixTimeSeconds()}&to={to.ToUnixTimeSeconds()}&count={ListPageSize}&skip={skip}";
            using var response = await SendAsync(action, ListUnavailableMessage, () => _http.GetAsync(url, cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(action, response, cancellationToken);
                throw new DomainException(ListUnavailableMessage, 502);
            }

            var page = await response.Content.ReadFromJsonAsync<CollectionWire<TWire>>(cancellationToken: cancellationToken);
            var items = page?.Items ?? [];
            all.AddRange(items.Select(map));
            if (items.Count < ListPageSize)
                return all;
        }
    }

    private const string ListUnavailableMessage = "Could not read payments from Razorpay. Try again.";

    /// <summary>A read, so a dropped connection or a Razorpay 5xx is retried before giving up.</summary>
    private async Task<PaymentWire?> FetchPaymentWireAsync(string paymentId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var last = attempt > FetchRetryDelays.Length;
            try
            {
                using var response = await _http.GetAsync($"v1/payments/{Uri.EscapeDataString(paymentId)}", cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;
                if ((int)response.StatusCode >= 500 && !last)
                {
                    await LogFailureAsync("fetch payment", response, cancellationToken);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    await LogFailureAsync("fetch payment", response, cancellationToken);
                    throw new DomainException(VerifyUnavailableMessage, 502);
                }
                else
                {
                    var payment = await response.Content.ReadFromJsonAsync<PaymentWire>(cancellationToken: cancellationToken);
                    if (payment is null || string.IsNullOrWhiteSpace(payment.Id) || string.IsNullOrWhiteSpace(payment.OrderId))
                        return null;
                    return payment;
                }
            }
            catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
            {
                _logger.LogWarning(ex, "Razorpay fetch payment {PaymentId} could not connect (attempt {Attempt}).", paymentId, attempt);
                if (last)
                    throw new DomainException(VerifyUnavailableMessage, 503);
            }

            await Task.Delay(FetchRetryDelays[attempt - 1], cancellationToken);
        }
    }

    private const string VerifyUnavailableMessage =
        "Your payment went through, but we couldn't confirm it with Razorpay yet. Don't pay again; try again in a moment.";

    private static readonly TimeSpan[] FetchRetryDelays = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2)];

    /// <summary>Writes are not retried: Razorpay may have acted on a request whose reply was lost.</summary>
    private async Task<HttpResponseMessage> SendAsync(
        string action,
        string unreachableMessage,
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Razorpay {Action} could not connect.", action);
            throw new DomainException(unreachableMessage, 503);
        }
    }

    private static bool IsUnreachable(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

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

    private sealed record RefundWire([property: JsonPropertyName("id")] string? Id);

    private sealed record PaymentWire(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("amount_refunded")] long AmountRefunded = 0);

    private sealed record RefundListWire(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("payment_id")] string PaymentId,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("receipt")] string? Receipt);

    private sealed record CollectionWire<T>([property: JsonPropertyName("items")] List<T>? Items);
}

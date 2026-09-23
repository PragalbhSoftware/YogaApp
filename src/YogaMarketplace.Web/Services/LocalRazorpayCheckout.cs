using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace YogaMarketplace.Web.Services;

public sealed class PaymentOptions
{
    public const string Section = "Payments";

    /// <summary>
    /// Development stand-in for Razorpay Checkout. The server signs the capture; the browser never sees <see cref="KeySecret"/>.
    /// Must stay false in Production. <c>Payments__UseFakeCheckout</c> overrides this for a later test checkout.
    /// </summary>
    public bool UseFakeCheckout { get; set; }

    /// <summary>
    /// HMAC secret for the local checkout only. Same placeholder as the API in Development. Never a live Razorpay secret.
    /// <c>Payments__KeySecret</c> overrides this. Do not commit test or live keys.
    /// </summary>
    public string KeySecret { get; set; } = "";
}

public interface ILocalRazorpayCheckout
{
    bool Enabled { get; }
    string CreatePaymentId();
    string Sign(string orderId, string paymentId);
}

/// <summary>
/// Signs <c>orderId|paymentId</c> the way the API's fake gateway checks it. Used only when <see cref="PaymentOptions.UseFakeCheckout"/> is on.
/// </summary>
public sealed class LocalRazorpayCheckout : ILocalRazorpayCheckout
{
    public const string PaymentIdPrefix = "pay_fake_";

    private readonly PaymentOptions _options;

    public LocalRazorpayCheckout(IOptions<PaymentOptions> options)
    {
        _options = options.Value;
    }

    public bool Enabled => _options.UseFakeCheckout;

    public string CreatePaymentId() => PaymentIdPrefix + Guid.NewGuid().ToString("N");

    public string Sign(string orderId, string paymentId)
    {
        if (string.IsNullOrWhiteSpace(_options.KeySecret))
            throw new InvalidOperationException("Payments:KeySecret is required when Payments:UseFakeCheckout is true.");
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId))
            throw new InvalidOperationException("Order id and payment id are required to sign a local checkout.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.KeySecret));
        var payload = Encoding.UTF8.GetBytes(orderId + "|" + paymentId);
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }
}

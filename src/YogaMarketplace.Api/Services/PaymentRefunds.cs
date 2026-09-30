using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Services;

internal static class PaymentRefunds
{
    /// <summary>Records the refund on <paramref name="payment"/> and sends it to Razorpay. Zero skips the gateway call.</summary>
    public static async Task RefundAsync(
        IRazorpayClient razorpay,
        Booking booking,
        Payment payment,
        decimal amount,
        CancellationToken cancellationToken)
    {
        PaymentRules.Refund(payment, amount);
        if (amount == 0)
            return;

        await razorpay.RefundPaymentAsync(
            payment.GatewayPaymentId!,
            RazorpayMoney.ToPaise(amount),
            booking.Id.ToString("N"),
            cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

internal static class PaymentRefunds
{
    /// <summary>
    /// Records the refund on <paramref name="payment"/> and saves every tracked change inside a transaction,
    /// then sends the refund to Razorpay and commits. Booking and payment status are concurrency tokens, so a
    /// second cancel of the same booking fails the save and never reaches Razorpay. A failed Razorpay call
    /// rolls everything back. Zero skips the gateway call.
    /// </summary>
    public static async Task RefundAndSaveAsync(
        YogaDbContext db,
        IRazorpayClient razorpay,
        ILogger logger,
        Booking booking,
        Payment payment,
        decimal amount,
        CancellationToken cancellationToken)
    {
        PaymentRules.Refund(payment, amount);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Refund for booking {BookingId} lost a concurrency race.", booking.Id);
            throw new DomainException("This booking was just changed. Refresh and try again.", 409);
        }

        // Once Razorpay is called the request must not be cancelled before the commit records it.
        if (amount > 0)
        {
            await razorpay.RefundPaymentAsync(
                payment.GatewayPaymentId!,
                RazorpayMoney.ToPaise(amount),
                booking.Id.ToString("N"),
                CancellationToken.None);
        }

        try
        {
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Refunded {Amount} on payment {PaymentId} for booking {BookingId} but the commit failed. Reconcile with Razorpay.",
                amount,
                payment.GatewayPaymentId,
                booking.Id);
            throw;
        }
    }
}

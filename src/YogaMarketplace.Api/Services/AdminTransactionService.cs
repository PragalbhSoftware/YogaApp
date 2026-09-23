using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminTransactionService
{
    Task<IReadOnlyList<AdminPaymentResponse>> ListPaymentsAsync(string? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminPayoutResponse>> ListPayoutsAsync(string? status, CancellationToken cancellationToken);
}

public class AdminTransactionService : IAdminTransactionService
{
    private static readonly PaymentStatus[] Settled =
    {
        PaymentStatus.Paid,
        PaymentStatus.Refunded,
        PaymentStatus.Failed
    };

    private readonly YogaDbContext _db;

    public AdminTransactionService(YogaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminPaymentResponse>> ListPaymentsAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Payments.AsNoTracking()
            .Include(p => p.Booking)
            .ThenInclude(b => b!.Provider)
            .Include(p => p.Booking)
            .ThenInclude(b => b!.Customer);

        IQueryable<Payment> filtered;
        if (string.IsNullOrWhiteSpace(status))
            filtered = query.Where(p => Settled.Contains(p.Status));
        else
        {
            var parsed = AdminQuery.ParseRequired<PaymentStatus>(status, "Status must be Paid, Refunded, or Failed.");
            if (!Settled.Contains(parsed))
                throw new DomainException("Status must be Paid, Refunded, or Failed.");
            filtered = query.Where(p => p.Status == parsed);
        }

        var currency = await _db.Policies.AsNoTracking().Select(p => p.Currency).SingleAsync(cancellationToken);
        var payments = await filtered.ToListAsync(cancellationToken);
        return payments
            .OrderByDescending(p => p.CreatedAt)
            .Take(AdminQuery.PageSize)
            .Select(p => ToPayment(p, currency))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminPayoutResponse>> ListPayoutsAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var parsed = string.IsNullOrWhiteSpace(status)
            ? PayoutStatus.Pending
            : AdminQuery.ParseRequired<PayoutStatus>(status, "Unknown payout status.");

        var rows = await (
                from payout in _db.PayoutsPending.AsNoTracking()
                join provider in _db.Providers.AsNoTracking() on payout.ProviderId equals provider.Id
                where payout.Status == parsed
                select new { payout, provider.DisplayName })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(row => row.payout.CreatedAt)
            .Take(AdminQuery.PageSize)
            .Select(row => new AdminPayoutResponse(
            row.payout.Id,
            row.payout.BookingId,
            row.payout.ProviderId,
            row.DisplayName,
            row.payout.GrossAmount,
            row.payout.FeePercent,
            row.payout.FeeAmount,
            row.payout.NetAmount,
            row.payout.Status.ToString(),
            row.payout.CreatedAt)).ToList();
    }

    private static AdminPaymentResponse ToPayment(Payment payment, string currency)
    {
        var booking = payment.Booking ?? throw new InvalidOperationException("Payment booking was not loaded.");
        var provider = booking.Provider ?? throw new InvalidOperationException("Provider was not loaded.");
        var customer = booking.Customer ?? throw new InvalidOperationException("Customer was not loaded.");
        return new AdminPaymentResponse(
            payment.Id,
            payment.BookingId,
            payment.Amount,
            currency,
            payment.Status.ToString(),
            payment.Gateway,
            payment.GatewayOrderId,
            payment.GatewayPaymentId,
            payment.CreatedAt,
            payment.UpdatedAt,
            provider.Id,
            provider.DisplayName,
            customer.Id,
            customer.Name,
            customer.Phone);
    }
}

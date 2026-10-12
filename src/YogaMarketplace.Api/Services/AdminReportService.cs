using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminReportService
{
    Task<AdminReportResponse> SummaryAsync(CancellationToken cancellationToken);
}

public class AdminReportService : IAdminReportService
{
    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;

    public AdminReportService(YogaDbContext db, IPlatformSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<AdminReportResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var grouped = await _db.Bookings.AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = grouped.ToDictionary(row => row.Key, row => row.Count);
        var byStatus = Enum.GetValues<BookingStatus>()
            .Select(status => new BookingStatusCount(status.ToString(), counts.GetValueOrDefault(status)))
            .ToList();

        // A booking paid with a credit gets a ₹0 Credit payment row; it is neither revenue nor a paid booking.
        var moneyPayments = _db.Payments.AsNoTracking()
            .Where(p => p.Gateway != PaymentGateways.Credit)
            .Where(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PartiallyRefunded);
        var gmv = await SumMoneyAsync(moneyPayments.Select(p => p.Amount - p.RefundedAmount), cancellationToken);
        var paidBookings = await moneyPayments.CountAsync(p => p.Booking!.CreditBatchId == null, cancellationToken);
        var creditBookings = await _db.Bookings.AsNoTracking().CountAsync(b => b.CreditBatchId != null, cancellationToken);
        var packSales = await SumMoneyAsync(
            _db.CreditBatches.AsNoTracking().Select(b => b.Price - b.RefundedAmount),
            cancellationToken);

        var pending = _db.PayoutsPending.AsNoTracking()
            .Where(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Exported);
        var payoutCount = await pending.CountAsync(cancellationToken);
        var gross = await SumMoneyAsync(pending.Select(p => p.GrossAmount), cancellationToken);
        var net = await SumMoneyAsync(pending.Select(p => p.NetAmount), cancellationToken);
        var currency = (await _settings.GetAsync(cancellationToken)).Currency;

        return new AdminReportResponse(
            byStatus,
            gmv,
            currency,
            new PendingPayoutTotals(payoutCount, gross, net),
            packSales,
            gmv + packSales,
            paidBookings,
            creditBookings);
    }

    // SQLite cannot SUM decimal. Both providers can SUM a real, and two decimal places cover INR.
    private static async Task<decimal> SumMoneyAsync(IQueryable<decimal> amounts, CancellationToken cancellationToken)
    {
        var total = await amounts.SumAsync(amount => (double?)amount, cancellationToken) ?? 0d;
        return decimal.Round((decimal)total, 2, MidpointRounding.AwayFromZero);
    }
}

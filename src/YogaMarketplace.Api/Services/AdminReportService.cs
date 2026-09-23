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

    public AdminReportService(YogaDbContext db)
    {
        _db = db;
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

        var gmv = await SumMoneyAsync(
            _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Paid).Select(p => p.Amount),
            cancellationToken);

        var pending = _db.PayoutsPending.AsNoTracking().Where(p => p.Status == PayoutStatus.Pending);
        var payoutCount = await pending.CountAsync(cancellationToken);
        var gross = await SumMoneyAsync(pending.Select(p => p.GrossAmount), cancellationToken);
        var net = await SumMoneyAsync(pending.Select(p => p.NetAmount), cancellationToken);
        var currency = await _db.Policies.AsNoTracking().Select(p => p.Currency).SingleAsync(cancellationToken);

        return new AdminReportResponse(byStatus, gmv, currency, new PendingPayoutTotals(payoutCount, gross, net));
    }

    // SQLite cannot SUM decimal. Both providers can SUM a real, and two decimal places cover INR.
    private static async Task<decimal> SumMoneyAsync(IQueryable<decimal> amounts, CancellationToken cancellationToken)
    {
        var total = await amounts.SumAsync(amount => (double?)amount, cancellationToken) ?? 0d;
        return decimal.Round((decimal)total, 2, MidpointRounding.AwayFromZero);
    }
}

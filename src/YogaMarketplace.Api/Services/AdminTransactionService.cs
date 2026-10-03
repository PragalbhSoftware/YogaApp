using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminTransactionService
{
    Task<IReadOnlyList<AdminPaymentResponse>> ListPaymentsAsync(string? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminPayoutResponse>> ListPayoutsAsync(string? status, CancellationToken cancellationToken);

    Task<AdminPayoutResponse> MarkPaidAsync(Guid id, CancellationToken cancellationToken);

    Task<PayoutCsv> DownloadPayoutsCsvAsync(string? status, CancellationToken cancellationToken);

    Task<PayoutCsv> ExportPendingPayoutsAsync(CancellationToken cancellationToken);
}

public record PayoutCsv(string FileName, byte[] Content, int Count);

public class AdminTransactionService : IAdminTransactionService
{
    private const string SettledMessage = "Status must be Paid, PartiallyRefunded, Refunded, or Failed.";

    private static readonly PaymentStatus[] Settled =
    {
        PaymentStatus.Paid,
        PaymentStatus.PartiallyRefunded,
        PaymentStatus.Refunded,
        PaymentStatus.Failed
    };

    private static readonly string[] CsvHeader =
    {
        "PayoutId", "BookingId", "Reason", "SessionDate", "Instructor", "Phone", "Email",
        "Gross", "FeePercent", "Fee", "Net", "Currency", "Status", "CreatedAt"
    };

    private readonly YogaDbContext _db;
    private readonly ILogger<AdminTransactionService> _logger;

    public AdminTransactionService(YogaDbContext db, ILogger<AdminTransactionService> logger)
    {
        _db = db;
        _logger = logger;
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
            var parsed = AdminQuery.ParseRequired<PaymentStatus>(status, SettledMessage);
            if (!Settled.Contains(parsed))
                throw new DomainException(SettledMessage);
            filtered = query.Where(p => p.Status == parsed);
        }

        var currency = await CurrencyAsync(cancellationToken);
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
        var parsed = ParsePayoutStatus(status);
        var rows = await PayoutRowsAsync(parsed, cancellationToken);
        return rows
            .OrderByDescending(row => row.Payout.CreatedAt)
            .Take(AdminQuery.PageSize)
            .Select(row => ToPayout(row.Payout, row.Provider.DisplayName))
            .ToList();
    }

    public async Task<AdminPayoutResponse> MarkPaidAsync(Guid id, CancellationToken cancellationToken)
    {
        var payout = await _db.PayoutsPending
            .Include(p => p.Booking)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new DomainException("Payout not found.", 404);

        if (payout.Status == PayoutStatus.Paid)
            throw new DomainException("This payout is already marked paid.", 409);
        if (payout.Status is not (PayoutStatus.Pending or PayoutStatus.Exported))
            throw new DomainException("Only pending payouts can be marked paid.");

        payout.Status = PayoutStatus.Paid;
        await _db.SaveChangesAsync(cancellationToken);

        var name = await _db.Providers.AsNoTracking()
            .Where(p => p.Id == payout.ProviderId)
            .Select(p => p.DisplayName)
            .SingleAsync(cancellationToken);

        return ToPayout(payout, name);
    }

    public async Task<PayoutCsv> DownloadPayoutsCsvAsync(string? status, CancellationToken cancellationToken)
    {
        var parsed = ParsePayoutStatus(status);
        var rows = await PayoutRowsAsync(parsed, cancellationToken);
        var currency = await CurrencyAsync(cancellationToken);
        return BuildCsv(rows, currency, $"payouts-{parsed.ToString().ToLowerInvariant()}");
    }

    /// <summary>
    /// Claims every Pending payout in one UPDATE, so two exports at the same moment never share a row.
    /// </summary>
    public async Task<PayoutCsv> ExportPendingPayoutsAsync(CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid();
        var claimed = await _db.PayoutsPending
            .Where(p => p.Status == PayoutStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.Status, PayoutStatus.Exported)
                    .SetProperty(p => p.ExportBatchId, (Guid?)batchId),
                cancellationToken);
        if (claimed == 0)
            throw new DomainException("There are no pending payouts to export.", 409);

        var rows = await PayoutRowsAsync(p => p.ExportBatchId == batchId, cancellationToken);
        var currency = await CurrencyAsync(cancellationToken);
        var csv = BuildCsv(rows, currency, "payouts-export");
        _logger.LogInformation("Exported {Count} pending payouts in batch {BatchId} for manual transfer.", csv.Count, batchId);
        return csv;
    }

    private Task<List<PayoutRow>> PayoutRowsAsync(PayoutStatus status, CancellationToken cancellationToken) =>
        PayoutRowsAsync(p => p.Status == status, cancellationToken);

    private async Task<List<PayoutRow>> PayoutRowsAsync(
        Expression<Func<PayoutPending, bool>> filter,
        CancellationToken cancellationToken)
    {
        var payouts = await _db.PayoutsPending.AsNoTracking()
            .Include(p => p.Booking!).ThenInclude(b => b.Slot)
            .Where(filter)
            .ToListAsync(cancellationToken);

        var providerIds = payouts.Select(p => p.ProviderId).Distinct().ToArray();
        var providers = await _db.Providers.AsNoTracking()
            .Include(p => p.User)
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return payouts.Select(p => new PayoutRow(p, providers[p.ProviderId])).ToList();
    }

    private static PayoutCsv BuildCsv(IReadOnlyList<PayoutRow> rows, string currency, string prefix)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', CsvHeader));
        foreach (var row in rows.OrderBy(r => r.Provider.DisplayName).ThenBy(r => r.Payout.CreatedAt))
        {
            var payout = row.Payout;
            var cells = new[]
            {
                payout.Id.ToString(),
                payout.BookingId.ToString(),
                ReasonFor(payout.Booking?.Status),
                payout.Booking?.Slot?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                row.Provider.DisplayName,
                LocalPhone(row.Provider.User?.Phone),
                row.Provider.User?.Email ?? "",
                Money(payout.GrossAmount),
                Money(payout.FeePercent),
                Money(payout.FeeAmount),
                Money(payout.NetAmount),
                currency,
                payout.Status.ToString(),
                payout.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)
            };
            builder.AppendLine(string.Join(',', cells.Select(CsvCell)));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return new PayoutCsv($"{prefix}-{stamp}.csv", bytes, rows.Count);
    }

    private static string ReasonFor(BookingStatus? status) => status switch
    {
        BookingStatus.Completed => "Completed",
        BookingStatus.NoShow => "No-show",
        BookingStatus.Cancelled => "Late cancel",
        _ => ""
    };

    /// <summary>"+919876543210" becomes "98765 43210" so spreadsheets keep it as text.</summary>
    private static string LocalPhone(string? phone) =>
        phone is { Length: 13 } && phone.StartsWith("+91", StringComparison.Ordinal)
            ? $"{phone[3..8]} {phone[8..]}"
            : phone ?? "";

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Quotes every cell and neutralises spreadsheet formulas.</summary>
    private static string CsvCell(string value)
    {
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static PayoutStatus ParsePayoutStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? PayoutStatus.Pending
            : AdminQuery.ParseRequired<PayoutStatus>(status, "Unknown payout status.");

    private Task<string> CurrencyAsync(CancellationToken cancellationToken) =>
        _db.Policies.AsNoTracking().Select(p => p.Currency).SingleAsync(cancellationToken);

    private static AdminPayoutResponse ToPayout(PayoutPending payout, string providerName) => new(
        payout.Id,
        payout.BookingId,
        payout.ProviderId,
        providerName,
        payout.GrossAmount,
        payout.FeePercent,
        payout.FeeAmount,
        payout.NetAmount,
        payout.Status.ToString(),
        payout.CreatedAt,
        payout.Booking?.Status.ToString() ?? "");

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
            customer.Phone,
            payment.RefundedAmount);
    }

    private sealed record PayoutRow(PayoutPending Payout, Provider Provider);
}

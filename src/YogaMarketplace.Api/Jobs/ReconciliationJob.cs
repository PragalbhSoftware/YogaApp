using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Jobs;

/// <summary>
/// Compares Razorpay with the database for one market day and stores what differs as <see cref="ReconciliationItem"/>s.
/// Runs daily at the admin-set time and covers the previous market day. It only reports: money is never changed here.
/// A Razorpay or database failure stores a Failed run with no items and rethrows, so the job runner retries.
/// </summary>
public class ReconciliationJob : IBackgroundJobHandler
{
    public const string JobType = "payments.reconcile";
    private const string DateFormat = "yyyy-MM-dd";
    private const int ErrorMax = 1000;

    /// <summary>Each run covers the market day before the one it runs on.</summary>
    public static readonly RecurringJob Recurring = new(
        JobType,
        terms => terms.Reconciliation,
        runAt => MarketClock.Today(runAt).AddDays(-1).ToString(DateFormat, CultureInfo.InvariantCulture));

    private readonly YogaDbContext _db;
    private readonly IRazorpayClient _razorpay;
    private readonly IPlatformSettingsService _settings;
    private readonly ILogger<ReconciliationJob> _logger;
    private readonly TimeProvider _time;

    public ReconciliationJob(
        YogaDbContext db,
        IRazorpayClient razorpay,
        IPlatformSettingsService settings,
        ILogger<ReconciliationJob> logger,
        TimeProvider time)
    {
        _db = db;
        _razorpay = razorpay;
        _settings = settings;
        _logger = logger;
        _time = time;
    }

    public string Type => JobType;

    public async Task RunAsync(string? payload, CancellationToken cancellationToken)
    {
        var marketDate = DateOnly.ParseExact(payload ?? "", DateFormat, CultureInfo.InvariantCulture);
        var done = await _db.ReconciliationRuns.AnyAsync(
            r => r.MarketDate == marketDate && r.Status == ReconciliationRunStatus.Completed,
            cancellationToken);
        if (!done)
            await ReconcileAsync(marketDate, _time.GetUtcNow(), cancellationToken);
    }

    public async Task<ReconciliationRun> ReconcileAsync(DateOnly marketDate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (start, end) = ReconciliationRules.WindowFor(marketDate);
        var run = new ReconciliationRun
        {
            Id = Guid.NewGuid(),
            MarketDate = marketDate,
            WindowStart = start,
            WindowEnd = end,
            StartedAt = now,
            GatewayCompared = _razorpay is not FakeRazorpayClient
        };

        var items = new List<ReconciliationItem>();
        try
        {
            var terms = (await _settings.GetAsync(cancellationToken)).Reconciliation;
            if (run.GatewayCompared)
                items.AddRange(await CompareGatewayAsync(start, end, cancellationToken));
            else
                _logger.LogInformation("Reconciliation for {MarketDate} skipped the Razorpay comparison because the fake gateway is in use.", marketDate);
            items.AddRange(await StuckWebhooksAsync(now, terms, cancellationToken));
            items.AddRange(await PendingRefundsAsync(now, terms, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _db.ChangeTracker.Clear();
            run.Status = ReconciliationRunStatus.Failed;
            run.Error = ex.Message.Length > ErrorMax ? ex.Message[..ErrorMax] : ex.Message;
            run.CompletedAt = _time.GetUtcNow();
            _db.ReconciliationRuns.Add(run);
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(ex, "Reconciliation run {RunId} for {MarketDate} failed; the job will retry.", run.Id, marketDate);
            throw;
        }

        foreach (var item in items)
        {
            item.Id = Guid.NewGuid();
            item.RunId = run.Id;
            item.CreatedAt = now;
        }
        run.Status = ReconciliationRunStatus.Completed;
        run.ItemCount = items.Count;
        run.CompletedAt = _time.GetUtcNow();
        _db.ReconciliationRuns.Add(run);
        _db.ReconciliationItems.AddRange(items);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            var saved = await _db.ReconciliationRuns.AsNoTracking().SingleOrDefaultAsync(
                r => r.MarketDate == marketDate && r.Status == ReconciliationRunStatus.Completed,
                CancellationToken.None);
            if (saved is null)
                throw;
            _logger.LogInformation(ex, "Reconciliation for {MarketDate} already has completed run {RunId}; this run's report was not saved.",
                marketDate, saved.Id);
            return saved;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("Reconciliation run {RunId} for {MarketDate} found nothing to check.", run.Id, marketDate);
            return run;
        }

        _logger.LogInformation("Reconciliation run {RunId} for {MarketDate} found {ItemCount} items to check: {ItemTypes}.",
            run.Id, marketDate, items.Count, CountsByType(items));
        var needAction = items.Where(i => ReconciliationRules.NeedsAction(i.Type)).ToList();
        if (needAction.Count > 0)
        {
            _logger.LogError("Reconciliation run {RunId} for {MarketDate} found {ActionCount} items that need action: {ActionItems}.",
                run.Id, marketDate, needAction.Count, IdsByType(needAction));
        }
        return run;
    }

    private static string CountsByType(IEnumerable<ReconciliationItem> items) =>
        string.Join(", ", items.GroupBy(i => i.Type).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));

    /// <summary>The Razorpay refund or payment id (or the item's reference when it has neither) per type. No amounts.</summary>
    private static string IdsByType(IEnumerable<ReconciliationItem> items) =>
        string.Join("; ", items.GroupBy(i => i.Type).OrderBy(g => g.Key).Select(g =>
            $"{g.Key}: {string.Join(", ", g.Select(i => i.GatewayRefundId ?? i.GatewayPaymentId ?? i.Reference))}"));

    private async Task<List<ReconciliationItem>> CompareGatewayAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var payments = await _razorpay.ListPaymentsAsync(start, end, cancellationToken);
        var refunds = await _razorpay.ListRefundsAsync(start, end, cancellationToken);
        var ledger = await LoadLedgerAsync(cancellationToken);
        var items = new List<ReconciliationItem>();

        var refundFlagged = new HashSet<string>(StringComparer.Ordinal);
        var ourReceipts = await OurReceiptsAsync(refunds, cancellationToken);
        foreach (var refund in refunds)
        {
            if (string.Equals(refund.Status, RazorpayWire.FailedStatus, StringComparison.OrdinalIgnoreCase))
                continue;
            ledger.TryGetValue(refund.PaymentId, out var row);
            if (row is not null && IsRecorded(row, refund))
                continue;
            refundFlagged.Add(refund.PaymentId);
            var ours = refund.Receipt is not null && ourReceipts.Contains(refund.Receipt);
            items.Add(new ReconciliationItem
            {
                Type = ours ? ReconciliationItemType.RefundNotRecorded : ReconciliationItemType.DashboardRefund,
                GatewayPaymentId = refund.PaymentId,
                GatewayRefundId = refund.Id,
                Reference = refund.Receipt,
                DatabaseValue = row is null ? null : Money(row.Refunded),
                GatewayValue = Money(RazorpayMoney.FromPaise(refund.AmountPaise)),
                Detail = ours
                    ? "Razorpay refunded this payment for us but the database has no record of it."
                    : row is null
                        ? "Razorpay has a refund on a payment the database does not know."
                        : "Razorpay has a refund made outside the app, for example from the dashboard."
            });
        }

        var remote = payments.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var row in ledger.Values.Where(r => r.CreatedAt >= start && r.CreatedAt < end && !remote.ContainsKey(r.GatewayPaymentId)))
        {
            var fetched = await _razorpay.FetchPaymentAsync(row.GatewayPaymentId, cancellationToken);
            if (fetched is not null)
            {
                remote[fetched.Id] = fetched;
                continue;
            }
            items.Add(new ReconciliationItem
            {
                Type = ReconciliationItemType.MissingInRazorpay,
                GatewayPaymentId = row.GatewayPaymentId,
                Reference = row.Reference,
                DatabaseValue = $"{Money(row.Amount)} {row.Status}",
                Detail = $"The database has this {row.Source} payment but Razorpay does not."
            });
        }

        foreach (var payment in remote.Values)
            items.AddRange(ComparePayment(payment, ledger.GetValueOrDefault(payment.Id), refundFlagged));
        return items;
    }

    private static IEnumerable<ReconciliationItem> ComparePayment(RazorpayPaymentRecord payment, LedgerRow? row, HashSet<string> refundFlagged)
    {
        var captured = IsCaptured(payment.Status);
        if (row is null)
        {
            if (captured)
            {
                yield return new ReconciliationItem
                {
                    Type = ReconciliationItemType.MissingInDatabase,
                    GatewayPaymentId = payment.Id,
                    Reference = payment.OrderId,
                    GatewayValue = $"{Money(RazorpayMoney.FromPaise(payment.AmountPaise))} {payment.Status}",
                    Detail = "Razorpay captured this payment but there is no booking, pack sale or refunded checkout for it."
                };
            }
            yield break;
        }

        var dbCaptured = row.Status is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded;
        if (captured != dbCaptured)
        {
            yield return Item(ReconciliationItemType.StatusMismatch, row.Status.ToString(), payment.Status ?? "unknown",
                "Razorpay and the database disagree on whether this payment was captured.");
        }
        if (payment.AmountPaise != Paise(row.Amount))
        {
            yield return Item(ReconciliationItemType.AmountMismatch, Money(row.Amount), Money(RazorpayMoney.FromPaise(payment.AmountPaise)),
                "Razorpay charged a different amount than the database recorded.");
        }
        if (captured && dbCaptured && payment.AmountRefundedPaise != Paise(row.Refunded) && !refundFlagged.Contains(payment.Id))
        {
            yield return Item(ReconciliationItemType.RefundMismatch, Money(row.Refunded), Money(RazorpayMoney.FromPaise(payment.AmountRefundedPaise)),
                "Razorpay refunded a different amount than the database recorded.");
        }

        ReconciliationItem Item(ReconciliationItemType type, string database, string gateway, string detail) => new()
        {
            Type = type,
            GatewayPaymentId = payment.Id,
            Reference = row.Reference,
            DatabaseValue = database,
            GatewayValue = gateway,
            Detail = detail
        };
    }

    /// <summary>A refund we stored by id, or, for rows that keep no refund id, one whose amount matches what we refunded.</summary>
    private static bool IsRecorded(LedgerRow row, RazorpayRefundRecord refund) =>
        row.RefundId is not null
            ? string.Equals(row.RefundId, refund.Id, StringComparison.Ordinal)
            : row.Refunded > 0 && Paise(row.Refunded) == refund.AmountPaise;

    private static bool IsCaptured(string? status) =>
        string.Equals(status, RazorpayWire.CapturedStatus, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, RazorpayWire.RefundedStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every Razorpay payment the database knows: booking payments, pack sales, and checkouts refunded without a booking.
    /// </summary>
    // ponytail: loads every gateway payment row because SQLite tests can't filter DateTimeOffset in SQL; window it in SQL if this grows.
    private async Task<Dictionary<string, LedgerRow>> LoadLedgerAsync(CancellationToken cancellationToken)
    {
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.GatewayPaymentId != null && p.Gateway != PaymentGateways.Credit)
            .Select(p => new LedgerRow(
                p.GatewayPaymentId!, "booking", p.BookingId.ToString(), p.Amount, p.RefundedAmount, p.Status, p.GatewayRefundId, p.CreatedAt))
            .ToListAsync(cancellationToken);
        var packs = await _db.CreditBatches.AsNoTracking()
            .Select(b => new { b.GatewayPaymentId, b.Id, b.Price, b.RefundedAmount, b.GatewayRefundId, b.PurchasedAt })
            .ToListAsync(cancellationToken);
        var checkouts = await _db.CheckoutIntents.AsNoTracking()
            .Where(c => c.Status == CheckoutStatus.Refunded && c.GatewayPaymentId != null)
            .Select(c => new { c.GatewayPaymentId, c.Id, c.Amount, c.ConvenienceFee, c.CreatedAt, c.CompletedAt })
            .ToListAsync(cancellationToken);

        var rows = payments
            .Concat(packs.Select(b => new LedgerRow(
                b.GatewayPaymentId, "pack", b.Id.ToString(), b.Price, b.RefundedAmount, PackStatus(b.Price, b.RefundedAmount), b.GatewayRefundId, b.PurchasedAt)))
            .Concat(checkouts.Select(c => new LedgerRow(
                c.GatewayPaymentId!, "checkout", c.Id.ToString(), c.Amount + c.ConvenienceFee, c.Amount + c.ConvenienceFee,
                PaymentStatus.Refunded, null, c.CompletedAt ?? c.CreatedAt)));
        var ledger = new Dictionary<string, LedgerRow>(StringComparer.Ordinal);
        foreach (var row in rows)
            ledger.TryAdd(row.GatewayPaymentId, row);
        return ledger;
    }

    private static PaymentStatus PackStatus(decimal price, decimal refunded) =>
        refunded <= 0m ? PaymentStatus.Paid : refunded >= price ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

    /// <summary>Refund receipts that are one of our booking, pack or checkout ids: refunds the app itself started.</summary>
    private async Task<HashSet<string>> OurReceiptsAsync(IReadOnlyList<RazorpayRefundRecord> refunds, CancellationToken cancellationToken)
    {
        var ids = refunds
            .Select(r => Guid.TryParseExact(r.Receipt, "N", out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
            return [];
        var known = await _db.Bookings.Where(b => ids.Contains(b.Id)).Select(b => b.Id)
            .Concat(_db.CreditBatches.Where(b => ids.Contains(b.Id)).Select(b => b.Id))
            .Concat(_db.CheckoutIntents.Where(c => ids.Contains(c.Id)).Select(c => c.Id))
            .ToListAsync(cancellationToken);
        return known.Select(id => id.ToString("N")).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<List<ReconciliationItem>> StuckWebhooksAsync(DateTimeOffset now, ReconciliationTerms terms, CancellationToken cancellationToken)
    {
        var cutoff = now.AddMinutes(-terms.WebhookStuckMinutes);
        var received = await _db.WebhookEvents.AsNoTracking()
            .Where(e => e.Status == WebhookEventStatus.Received)
            .Select(e => new { e.EventId, e.EventType, e.GatewayPaymentId, e.ReceivedAt })
            .ToListAsync(cancellationToken);
        var open = await OpenReferencesAsync(ReconciliationItemType.StuckWebhook, cancellationToken);
        return received
            .Where(e => e.ReceivedAt < cutoff && !open.Contains(e.EventId))
            .Select(e => new ReconciliationItem
            {
                Type = ReconciliationItemType.StuckWebhook,
                GatewayPaymentId = e.GatewayPaymentId,
                Reference = e.EventId,
                Detail = $"{e.EventType} webhook received {e.ReceivedAt.ToString("u", CultureInfo.InvariantCulture)} was never processed."
            })
            .ToList();
    }

    private async Task<List<ReconciliationItem>> PendingRefundsAsync(DateTimeOffset now, ReconciliationTerms terms, CancellationToken cancellationToken)
    {
        var cutoff = now.AddHours(-terms.RefundPendingHours);
        var pending = await _db.Payments.AsNoTracking()
            .Where(p => p.RefundStatus == RefundStatus.Pending)
            .Select(p => new { p.BookingId, p.GatewayPaymentId, p.GatewayRefundId, p.RefundedAmount, p.CreatedAt, p.UpdatedAt })
            .ToListAsync(cancellationToken);
        var open = await OpenReferencesAsync(ReconciliationItemType.RefundPending, cancellationToken);
        return pending
            .Where(p => (p.UpdatedAt ?? p.CreatedAt) < cutoff && !open.Contains(p.BookingId.ToString()))
            .Select(p => new ReconciliationItem
            {
                Type = ReconciliationItemType.RefundPending,
                GatewayPaymentId = p.GatewayPaymentId,
                GatewayRefundId = p.GatewayRefundId,
                Reference = p.BookingId.ToString(),
                DatabaseValue = Money(p.RefundedAmount),
                Detail = "This refund is still pending; Razorpay has not confirmed it was processed."
            })
            .ToList();
    }

    /// <summary>References of items of this type still open from earlier runs, so the same problem is not reported every day.</summary>
    private async Task<HashSet<string>> OpenReferencesAsync(ReconciliationItemType type, CancellationToken cancellationToken)
    {
        var references = await _db.ReconciliationItems.AsNoTracking()
            .Where(i => i.Type == type && i.Status == ReconciliationItemStatus.Open && i.Reference != null)
            .Select(i => i.Reference!)
            .ToListAsync(cancellationToken);
        return references.ToHashSet(StringComparer.Ordinal);
    }

    private static long Paise(decimal rupees) => (long)decimal.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed record LedgerRow(
        string GatewayPaymentId,
        string Source,
        string Reference,
        decimal Amount,
        decimal Refunded,
        PaymentStatus Status,
        string? RefundId,
        DateTimeOffset CreatedAt);
}

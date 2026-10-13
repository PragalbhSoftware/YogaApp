namespace YogaMarketplace.Domain;

public enum ReconciliationRunStatus
{
    Completed,
    /// <summary>Razorpay or the database failed mid-run. No items are kept; the job retries.</summary>
    Failed,
    /// <summary>An older completed run for a day that has a newer one. Only set when the one-per-day rule was added.</summary>
    Superseded
}

public enum ReconciliationItemType
{
    MissingInDatabase,
    MissingInRazorpay,
    AmountMismatch,
    StatusMismatch,
    RefundMismatch,
    /// <summary>Razorpay has a refund we started (our receipt) but the database never recorded it.</summary>
    RefundNotRecorded,
    /// <summary>Razorpay has a refund made outside the app, for example from the Razorpay dashboard.</summary>
    DashboardRefund,
    StuckWebhook,
    RefundPending
}

public enum ReconciliationItemStatus
{
    Open,
    Resolved
}

/// <summary>One daily comparison of Razorpay against the database for one market day. Reports only; never changes money.</summary>
public class ReconciliationRun
{
    public Guid Id { get; set; }
    public DateOnly MarketDate { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ReconciliationRunStatus Status { get; set; }

    /// <summary>False when the fake gateway is in use and only the database checks ran.</summary>
    public bool GatewayCompared { get; set; }
    public int ItemCount { get; set; }
    public string? Error { get; set; }
}

public class ReconciliationItem
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public ReconciliationItemType Type { get; set; }
    public ReconciliationItemStatus Status { get; set; } = ReconciliationItemStatus.Open;
    public string? GatewayPaymentId { get; set; }
    public string? GatewayRefundId { get; set; }

    /// <summary>What the item is about when it is not a payment, such as a webhook event id.</summary>
    public string? Reference { get; set; }
    public string? DatabaseValue { get; set; }
    public string? GatewayValue { get; set; }
    public string Detail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}

public static class ReconciliationRules
{
    public const int NoteMax = 500;
    public const int DetailMax = 500;
    public const string OneCompletedRunPerDayIndex = "IX_ReconciliationRuns_MarketDate_Completed";

    /// <summary>The run on <paramref name="marketDate"/> covers that whole market day, midnight to midnight.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) WindowFor(DateOnly marketDate) =>
        (MumbaiClock.SessionStart(marketDate, TimeOnly.MinValue), MumbaiClock.SessionStart(marketDate.AddDays(1), TimeOnly.MinValue));

    /// <summary>The next market-clock moment at <paramref name="runAt"/> that is after <paramref name="now"/>.</summary>
    public static DateTimeOffset NextRunAfter(DateTimeOffset now, TimeOnly runAt)
    {
        var today = MumbaiClock.Today(now);
        var run = MumbaiClock.SessionStart(today, runAt);
        return run > now ? run : MumbaiClock.SessionStart(today.AddDays(1), runAt);
    }

    public static void Resolve(ReconciliationItem item, Guid adminUserId, string? note, DateTimeOffset now)
    {
        var trimmed = note?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException("Add a note saying how this was resolved.");
        if (trimmed.Length > NoteMax)
            throw new DomainException($"Note must be {NoteMax} characters or less.");
        if (item.Status == ReconciliationItemStatus.Resolved)
            throw new DomainException("This item is already resolved.", 409);
        item.Status = ReconciliationItemStatus.Resolved;
        item.ResolvedAt = now;
        item.ResolvedByUserId = adminUserId;
        item.ResolutionNote = trimmed;
    }
}

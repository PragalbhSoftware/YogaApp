namespace YogaMarketplace.Domain;

/// <summary>A platform-wide pack of session credits. Never deleted; a deactivated pack can't be bought but bought credits stay usable.</summary>
public class CreditPack
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Sessions { get; set; }
    public decimal Price { get; set; }
    public int ValidityDays { get; set; }

    /// <summary>A credit covers a session whose regular price is at or below this amount.</summary>
    public decimal PriceCap { get; set; }

    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Credit rules copied onto each purchase, so a later settings change never applies to credits already bought.</summary>
public sealed record CreditTerms(
    bool ChargeConvenienceFee,
    CreditPayBasis PayBasis,
    CreditLateCancel LateCancel,
    bool BookingStandsAfterExpiry,
    bool RefundsEnabled,
    decimal ReferenceSessionPrice,
    decimal RefundFee);

/// <summary>
/// One purchase of a <see cref="CreditPack"/>. Holds the pack details and <see cref="CreditTerms"/> as they were at purchase.
/// <see cref="Remaining"/> always equals the sum of this batch's ledger rows; both change together under the customer's
/// <see cref="CreditAccount"/> lock.
/// </summary>
public class CreditBatch
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid PackId { get; set; }
    public Guid CheckoutIntentId { get; set; }
    public string PackName { get; set; } = "";
    public int Sessions { get; set; }
    public decimal Price { get; set; }
    public int ValidityDays { get; set; }
    public decimal PriceCap { get; set; }
    public string Currency { get; set; } = "INR";
    public bool ChargeConvenienceFee { get; set; }
    public CreditPayBasis PayBasis { get; set; } = CreditPayBasis.PackPerSession;
    public CreditLateCancel LateCancel { get; set; } = CreditLateCancel.PayInstructorShare;
    public bool BookingStandsAfterExpiry { get; set; } = true;
    public bool RefundsEnabled { get; set; } = true;
    public decimal ReferenceSessionPrice { get; set; }
    public decimal RefundFee { get; set; }
    public string GatewayPaymentId { get; set; } = "";
    public DateTimeOffset PurchasedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Remaining { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    public CreditTerms Terms => new(
        ChargeConvenienceFee, PayBasis, LateCancel, BookingStandsAfterExpiry, RefundsEnabled, ReferenceSessionPrice, RefundFee);
}

/// <summary>One movement of credits in a batch. Unique filtered indexes allow one Use and one Return per booking and one Purchase, Expire and Refund per batch.</summary>
public class CreditLedgerEntry
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid CustomerId { get; set; }
    public CreditEntryKind Kind { get; set; }

    /// <summary>Signed change to the batch. A return to an expired or refunded batch is recorded with zero.</summary>
    public int Credits { get; set; }

    public Guid? BookingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// One row per customer. Every change to the customer's credits bumps <see cref="Version"/>, a concurrency token, so two
/// requests racing for the last credit can't both save.
/// </summary>
public class CreditAccount
{
    public Guid CustomerId { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class CreditPackRules
{
    public const int NameMax = 80;
    public const int MaxSessions = 100;
    public const int MaxValidityDays = 730;
    public const decimal MaxPrice = 500_000m;

    public static void Validate(string name, int sessions, decimal price, int validityDays, decimal priceCap)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMax)
            throw new DomainException($"Give the pack a name of 1 to {NameMax} characters.");
        if (sessions is < 1 or > MaxSessions)
            throw new DomainException($"Sessions must be between 1 and {MaxSessions}.");
        if (price < 1 || price > MaxPrice || decimal.Round(price, 2) != price)
            throw new DomainException($"Price must be between ₹1 and ₹{MaxPrice:0} with at most 2 decimals.");
        if (validityDays is < 1 or > MaxValidityDays)
            throw new DomainException($"Validity must be between 1 and {MaxValidityDays} days.");
        if (priceCap <= 0 || priceCap > MaxPrice || decimal.Round(priceCap, 2) != priceCap)
            throw new DomainException("Price cap must be above zero with at most 2 decimals.");
    }
}

/// <summary>
/// Credit rules. Credits are used from the batch that expires first. A batch expires at midnight India time after its
/// validity days, counting the purchase day as day 1. A trial can never be paid with a credit.
/// </summary>
public static class CreditRules
{
    public const string NoneLeftMessage = "You have no credits that cover this session.";
    public const string LastCreditTakenMessage = "Your last credit was just used on another booking. You can pay for this session instead.";
    public const string BlockedMessage = "Your account can't buy packs or use credits right now.";

    public static DateTimeOffset ExpiresAt(DateTimeOffset purchasedAt, int validityDays) =>
        MumbaiClock.SessionStart(MumbaiClock.Today(purchasedAt).AddDays(validityDays), TimeOnly.MinValue);

    /// <summary>The last India date a credit from this batch can be used on.</summary>
    public static DateOnly LastDay(CreditBatch batch) => MumbaiClock.Today(batch.ExpiresAt).AddDays(-1);

    public static decimal PerSessionValue(CreditBatch batch) =>
        decimal.Round(batch.Price / batch.Sessions, 2, MidpointRounding.AwayFromZero);

    /// <summary>The instructor's gross for a credit session, frozen on the booking as its amount.</summary>
    public static decimal InstructorGross(CreditBatch batch, decimal sessionRate) =>
        batch.PayBasis == CreditPayBasis.SessionPrice ? sessionRate : PerSessionValue(batch);

    public static bool IsUsable(CreditBatch batch, DateTimeOffset now) =>
        batch.Remaining > 0 && batch.RefundedAt is null && batch.ExpiresAt > now;

    public static bool Covers(CreditBatch batch, decimal sessionRate) => sessionRate <= batch.PriceCap;

    /// <summary>With <see cref="CreditBatch.BookingStandsAfterExpiry"/> off, only sessions before the expiry can be booked.</summary>
    public static bool AllowsSession(CreditBatch batch, DateTimeOffset sessionStart) =>
        batch.BookingStandsAfterExpiry || sessionStart < batch.ExpiresAt;

    public static CreditChoice Choose(IEnumerable<CreditBatch> batches, decimal sessionRate, DateTimeOffset sessionStart, DateTimeOffset now)
    {
        var usable = batches.Where(b => IsUsable(b, now)).OrderBy(b => b.ExpiresAt).ThenBy(b => b.PurchasedAt).ToList();
        if (usable.Count == 0)
            return new CreditChoice(null, CreditUnavailable.NoCredits);
        var covering = usable.Where(b => Covers(b, sessionRate)).ToList();
        if (covering.Count == 0)
            return new CreditChoice(null, CreditUnavailable.AboveCap);
        var batch = covering.FirstOrDefault(b => AllowsSession(b, sessionStart));
        return batch is null
            ? new CreditChoice(null, CreditUnavailable.ExpiresBeforeSession)
            : new CreditChoice(batch, null);
    }

    /// <summary>
    /// Admin partial refund: unused credits times the reference session price, minus the refund fee, never negative and
    /// never more than what is left of the amount paid. Under ₹1 Razorpay can't refund, so it rounds down to zero.
    /// </summary>
    public static decimal RefundFor(CreditBatch batch)
    {
        var gross = batch.Remaining * batch.ReferenceSessionPrice - batch.RefundFee;
        var amount = decimal.Round(Math.Clamp(gross, 0m, batch.Price - batch.RefundedAmount), 2, MidpointRounding.AwayFromZero);
        return amount < 1m ? 0m : amount;
    }

    /// <summary>Marks a new booking as paid with a credit from <paramref name="batch"/> and freezes the credit's worth on it.</summary>
    public static void Attach(Booking booking, CreditBatch batch)
    {
        if (booking.IsTrial)
            throw new DomainException("A trial class can't be paid with a credit.", 409);
        booking.CreditBatchId = batch.Id;
        booking.CreditValue = PerSessionValue(batch);
    }

    /// <summary>
    /// Customer cancel of a credit booking. Outside the late window the credit and any fee come back. Inside it the fee is
    /// kept and the batch's saved <see cref="CreditLateCancel"/> rule decides the credit and the instructor's share.
    /// </summary>
    public static CreditCancelSettlement SettleCustomerCancel(
        Booking booking,
        CreditBatch batch,
        decimal feePaid,
        DateTimeOffset sessionStart,
        DateTimeOffset now)
    {
        if (!BookingRules.IsLateCancel(booking, sessionStart, now))
            return new CreditCancelSettlement(true, 0m, feePaid);
        return batch.LateCancel switch
        {
            CreditLateCancel.PayInstructorShare => new CreditCancelSettlement(false, BookingRules.LateCancelFeeFor(booking, sessionStart, now), 0m),
            CreditLateCancel.ReturnCredit => new CreditCancelSettlement(true, 0m, 0m),
            _ => new CreditCancelSettlement(false, 0m, 0m)
        };
    }

    /// <summary>
    /// Why a cancel won't give a usable credit back, or null if it will. A refunded or expired batch takes a returned
    /// credit as zero, the same as the ledger does when the cancel goes through.
    /// </summary>
    public static string? NoCreditBackReason(CreditBatch batch, CreditCancelSettlement settlement, DateTimeOffset now)
    {
        if (batch.RefundedAt is not null)
            return CreditNotReturned.PackRefunded;
        if (batch.ExpiresAt <= now)
            return CreditNotReturned.PackExpired;
        return settlement.ReturnCredit ? null : CreditNotReturned.LateCancel;
    }

    public static void EnsureRefundable(CreditBatch batch, DateTimeOffset now)
    {
        if (!batch.RefundsEnabled)
            throw new DomainException("Refunds were off when this pack was bought.", 409);
        if (batch.RefundedAt is not null)
            throw new DomainException("This pack was already refunded.", 409);
        if (batch.ExpiresAt <= now)
            throw new DomainException("This pack has expired.", 409);
        if (batch.Remaining <= 0)
            throw new DomainException("This pack has no unused credits to refund.", 409);
    }
}

public sealed record CreditChoice(CreditBatch? Batch, CreditUnavailable? Reason);

public sealed record CreditCancelSettlement(bool ReturnCredit, decimal InstructorShare, decimal FeeRefund);

public static class CreditNotReturned
{
    public const string LateCancel = "LateCancel";
    public const string PackRefunded = "PackRefunded";
    public const string PackExpired = "PackExpired";
}

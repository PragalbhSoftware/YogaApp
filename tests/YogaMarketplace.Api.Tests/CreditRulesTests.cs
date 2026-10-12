using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class CreditRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_batch_expires_at_midnight_India_time_counting_the_purchase_day_as_day_one()
    {
        // 23:59 IST on 12 Oct and 00:00 IST on 13 Oct are different India days.
        var lateEvening = new DateTimeOffset(2026, 10, 12, 18, 29, 0, TimeSpan.Zero);
        var justAfterMidnight = new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 11, 10, 18, 30, 0, TimeSpan.Zero), CreditRules.ExpiresAt(lateEvening, 30));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 18, 30, 0, TimeSpan.Zero), CreditRules.ExpiresAt(justAfterMidnight, 30));

        var batch = Batch(expiresAt: CreditRules.ExpiresAt(lateEvening, 30));
        Assert.Equal(new DateOnly(2026, 11, 10), CreditRules.LastDay(batch));
        Assert.True(CreditRules.IsUsable(batch, batch.ExpiresAt.AddSeconds(-1)));
        Assert.False(CreditRules.IsUsable(batch, batch.ExpiresAt));
    }

    [Fact]
    public void Choose_uses_the_earliest_expiry_that_covers_the_session()
    {
        var later = Batch(expiresAt: Now.AddDays(20), cap: 1000m);
        var sooner = Batch(expiresAt: Now.AddDays(5), cap: 1000m);
        var cheapCap = Batch(expiresAt: Now.AddDays(1), cap: 500m);
        var spent = Batch(expiresAt: Now.AddDays(1), remaining: 0);

        var choice = CreditRules.Choose([later, cheapCap, spent, sooner], 749m, Now.AddDays(2), Now);

        Assert.Same(sooner, choice.Batch);
        Assert.Null(choice.Reason);
    }

    [Fact]
    public void Choose_explains_why_no_credit_applies()
    {
        Assert.Equal(CreditUnavailable.NoCredits, CreditRules.Choose([], 749m, Now.AddDays(1), Now).Reason);
        Assert.Equal(CreditUnavailable.NoCredits, CreditRules.Choose([Batch(refunded: true)], 749m, Now.AddDays(1), Now).Reason);
        Assert.Equal(CreditUnavailable.AboveCap, CreditRules.Choose([Batch(cap: 700m)], 749m, Now.AddDays(1), Now).Reason);

        var ruleOff = Batch(expiresAt: Now.AddDays(3), standsAfterExpiry: false);
        Assert.Equal(CreditUnavailable.ExpiresBeforeSession, CreditRules.Choose([ruleOff], 749m, Now.AddDays(4), Now).Reason);
        Assert.Same(ruleOff, CreditRules.Choose([ruleOff], 749m, Now.AddDays(2), Now).Batch);

        var ruleOn = Batch(expiresAt: Now.AddDays(3), standsAfterExpiry: true);
        Assert.Same(ruleOn, CreditRules.Choose([ruleOn], 749m, Now.AddDays(4), Now).Batch);
    }

    [Fact]
    public void Credit_value_and_instructor_gross_follow_the_saved_pay_basis()
    {
        var perSession = Batch(price: 1000m, sessions: 3, payBasis: CreditPayBasis.PackPerSession);
        var sessionPrice = Batch(price: 1000m, sessions: 3, payBasis: CreditPayBasis.SessionPrice);

        Assert.Equal(333.33m, CreditRules.PerSessionValue(perSession));
        Assert.Equal(333.33m, CreditRules.InstructorGross(perSession, 749m));
        Assert.Equal(749m, CreditRules.InstructorGross(sessionPrice, 749m));
    }

    [Theory]
    [InlineData(5, 799, 100, 0, 3895)]
    [InlineData(8, 1000, 0, 0, 4800)]
    [InlineData(1, 50, 100, 0, 0)]
    [InlineData(5, 799, 100, 4500, 300)]
    [InlineData(1, 100.5, 100, 0, 0)]
    public void Partial_refund_is_unused_times_reference_minus_fee_capped_and_never_negative(
        int remaining, decimal reference, decimal fee, decimal alreadyRefunded, decimal expected)
    {
        var batch = Batch(price: 4800m, sessions: 8, remaining: remaining);
        batch.ReferenceSessionPrice = reference;
        batch.RefundFee = fee;
        batch.RefundedAmount = alreadyRefunded;

        Assert.Equal(expected, CreditRules.RefundFor(batch));
    }

    [Fact]
    public void A_refund_needs_refunds_on_unused_credits_and_no_earlier_refund()
    {
        CreditRules.EnsureRefundable(Batch(), Now);
        Assert.Throws<DomainException>(() => CreditRules.EnsureRefundable(Batch(refundsEnabled: false), Now));
        Assert.Throws<DomainException>(() => CreditRules.EnsureRefundable(Batch(refunded: true), Now));
        Assert.Throws<DomainException>(() => CreditRules.EnsureRefundable(Batch(remaining: 0), Now));
        Assert.Throws<DomainException>(() => CreditRules.EnsureRefundable(Batch(expiresAt: Now), Now));
    }

    [Theory]
    [InlineData(CreditLateCancel.PayInstructorShare, false, 300)]
    [InlineData(CreditLateCancel.KeepCredit, false, 0)]
    [InlineData(CreditLateCancel.ReturnCredit, true, 0)]
    public void A_late_cancel_follows_the_saved_rule_and_keeps_the_fee(CreditLateCancel rule, bool returned, decimal share)
    {
        var batch = Batch(lateCancel: rule);
        var booking = CreditBooking(BookingStatus.Upcoming);
        var start = Now.AddHours(2);

        var settlement = CreditRules.SettleCustomerCancel(booking, batch, 25m, start, Now);

        Assert.Equal(new CreditCancelSettlement(returned, share, 0m), settlement);
    }

    [Fact]
    public void An_early_or_pending_cancel_returns_the_credit_and_the_fee()
    {
        var batch = Batch(lateCancel: CreditLateCancel.PayInstructorShare);

        Assert.Equal(new CreditCancelSettlement(true, 0m, 25m),
            CreditRules.SettleCustomerCancel(CreditBooking(BookingStatus.Upcoming), batch, 25m, Now.AddDays(2), Now));
        Assert.Equal(new CreditCancelSettlement(true, 0m, 25m),
            CreditRules.SettleCustomerCancel(CreditBooking(BookingStatus.PendingAccept), batch, 25m, Now.AddHours(1), Now));
    }

    [Fact]
    public void A_trial_can_never_be_paid_with_a_credit()
    {
        var booking = CreditBooking(BookingStatus.PendingAccept);
        booking.IsTrial = true;

        var error = Assert.Throws<DomainException>(() => CreditRules.Attach(booking, Batch()));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public void Pack_rules_reject_bad_values()
    {
        CreditPackRules.Validate("8 sessions", 8, 4800m, 30, 800m);
        Assert.Throws<DomainException>(() => CreditPackRules.Validate(" ", 8, 4800m, 30, 800m));
        Assert.Throws<DomainException>(() => CreditPackRules.Validate("Pack", 0, 4800m, 30, 800m));
        Assert.Throws<DomainException>(() => CreditPackRules.Validate("Pack", 8, 0.5m, 30, 800m));
        Assert.Throws<DomainException>(() => CreditPackRules.Validate("Pack", 8, 4800.555m, 30, 800m));
        Assert.Throws<DomainException>(() => CreditPackRules.Validate("Pack", 8, 4800m, 0, 800m));
        Assert.Throws<DomainException>(() => CreditPackRules.Validate("Pack", 8, 4800m, 30, 0m));
    }

    [Fact]
    public void Credit_settings_are_validated_and_audited()
    {
        var settings = new PlatformSettings();
        var changes = PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(
            CreditConvenienceFee: true,
            CreditPayBasis: CreditPayBasis.SessionPrice,
            PackReferenceSessionPrice: 850m,
            PackRefundFee: 100m));

        Assert.Equal(
            ["CreditConvenienceFee", "CreditPayBasis", "PackReferenceSessionPrice", "PackRefundFee"],
            changes.Select(c => c.Field));
        Assert.Equal(CreditPayBasis.SessionPrice, PlatformSettingsRules.CreditTermsFor(settings).PayBasis);
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(PackRefundFee: -1m)));
        Assert.Throws<DomainException>(() => PlatformSettingsRules.Apply(settings, new PlatformSettingsChanges(CreditLateCancel: (CreditLateCancel)9)));
    }

    private static CreditBatch Batch(
        DateTimeOffset? expiresAt = null,
        decimal cap = 800m,
        int remaining = 5,
        decimal price = 4800m,
        int sessions = 8,
        bool refunded = false,
        bool refundsEnabled = true,
        bool standsAfterExpiry = true,
        CreditPayBasis payBasis = CreditPayBasis.PackPerSession,
        CreditLateCancel lateCancel = CreditLateCancel.PayInstructorShare) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        PackName = "Pack",
        Sessions = sessions,
        Price = price,
        PriceCap = cap,
        Remaining = remaining,
        PurchasedAt = Now.AddDays(-1),
        ExpiresAt = expiresAt ?? Now.AddDays(10),
        RefundedAt = refunded ? Now.AddHours(-1) : null,
        RefundsEnabled = refundsEnabled,
        BookingStandsAfterExpiry = standsAfterExpiry,
        PayBasis = payBasis,
        LateCancel = lateCancel,
        ReferenceSessionPrice = 799m
    };

    private static Booking CreditBooking(BookingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        Amount = 600m,
        CancelFreeWindowHours = 12,
        LateCancelFeeType = LateCancelFeeType.Percent,
        LateCancelFeeValue = 50m
    };
}

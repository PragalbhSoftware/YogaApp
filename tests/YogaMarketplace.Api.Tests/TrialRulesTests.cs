using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class TrialRulesTests
{
    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(199.999, 0, 200)]
    [InlineData(749, 0, 749)]
    [InlineData(0.5, 0.5, 0.5)]
    public void Trial_price_is_rounded_and_may_be_free_when_the_fee_covers_the_minimum(
        decimal price, decimal fee, decimal expected)
    {
        Assert.Equal(expected, TrialRules.CleanPrice(price, 749m, fee));
    }

    [Theory]
    [InlineData(-1, 749, 25)]
    [InlineData(750, 749, 25)]
    [InlineData(0, 749, 0)]
    [InlineData(0.99, 749, 0)]
    public void Trial_price_is_refused_when_negative_above_the_regular_fee_or_below_the_gateway_minimum(
        decimal price, decimal lowest, decimal fee)
    {
        Assert.Throws<DomainException>(() => TrialRules.CleanPrice(price, lowest, fee));
    }

    [Fact]
    public void A_trial_needs_a_regular_fee_first()
    {
        Assert.Throws<DomainException>(() => TrialRules.CleanPrice(0, null, 25));
    }

    [Theory]
    [InlineData(BookingStatus.PendingAccept, null, true)]
    [InlineData(BookingStatus.Upcoming, null, true)]
    [InlineData(BookingStatus.Completed, null, true)]
    [InlineData(BookingStatus.NoShow, null, true)]
    [InlineData(BookingStatus.Cancelled, CancelledBy.Customer, true)]
    [InlineData(BookingStatus.Cancelled, CancelledBy.Admin, false)]
    [InlineData(BookingStatus.Declined, null, false)]
    public void Only_an_instructor_decline_or_admin_cancel_gives_the_trial_back(
        BookingStatus status, CancelledBy? cancelledBy, bool usesTrial)
    {
        var booking = new Booking { IsTrial = true, Status = status, CancelledBy = cancelledBy };
        Assert.Equal(usesTrial, TrialRules.Consumes(booking));
        Assert.False(TrialRules.Consumes(new Booking { IsTrial = false, Status = status, CancelledBy = cancelledBy }));
    }

    [Fact]
    public void Status_changes_keep_the_stored_trial_flag_in_step()
    {
        var declined = PendingTrial();
        Assert.True(declined.TrialConsumed);
        BookingRules.Decline(declined);
        Assert.False(declined.TrialConsumed);

        var adminCancelled = PendingTrial();
        BookingRules.AdminCancel(adminCancelled, "Instructor unwell", DateTimeOffset.UtcNow);
        Assert.False(adminCancelled.TrialConsumed);

        var customerCancelled = PendingTrial();
        BookingRules.Cancel(customerCancelled, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        Assert.True(customerCancelled.TrialConsumed);

        var noShow = PendingTrial();
        BookingRules.Accept(noShow);
        BookingRules.MarkNoShow(noShow);
        Assert.True(noShow.TrialConsumed);
    }

    private static Booking PendingTrial()
    {
        var providerId = Guid.NewGuid();
        var service = new Service { Id = Guid.NewGuid(), ProviderId = providerId };
        var slot = new AvailabilitySlot { Id = Guid.NewGuid(), ProviderId = providerId, Mode = SessionMode.Online };
        return BookingRules.CreateAfterPayment(
            Guid.NewGuid(), service, slot, 0m, new BookingTerms(10m, 25m, 12, LateCancelFeeType.Percent, 50m), null, null, "https://meet.google.com/abc-defg-hij", null, isTrial: true);
    }

    [Fact]
    public void A_free_trial_below_the_gateway_minimum_cannot_be_charged()
    {
        TrialRules.EnsureChargeable(1m);
        var ex = Assert.Throws<DomainException>(() => TrialRules.EnsureChargeable(0m));
        Assert.Equal(409, ex.StatusCode);
    }
}

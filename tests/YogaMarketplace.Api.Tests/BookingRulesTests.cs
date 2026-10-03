using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class BookingRulesTests
{
    [Fact]
    public void PayAtBook_creates_pending_accept_and_requires_home_address()
    {
        var slot = Slot(SessionMode.Home);
        var service = ServiceFor(slot);

        var missing = Assert.Throws<DomainException>(() =>
            BookingRules.CreateAfterPayment(Guid.NewGuid(), service, slot, 899m, Terms, "Bandra West", null, null, null));
        Assert.Contains("landmark", missing.Message, StringComparison.OrdinalIgnoreCase);

        var booking = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), service, slot, 899m, Terms, "14th Road, Bandra West", "Near the station", null, null);

        Assert.Equal(BookingStatus.PendingAccept, booking.Status);
        Assert.Equal("14th Road, Bandra West", booking.HomeAddress);
        Assert.Equal(SessionMode.Home, booking.Mode);
        Assert.True(BookingRules.OccupiesSlot(booking.Status));
    }

    [Fact]
    public void Studio_and_online_skip_home_address_and_keep_their_location()
    {
        var studioSlot = Slot(SessionMode.Studio);
        var studio = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), ServiceFor(studioSlot), studioSlot, 749m, Terms, null, null, null, "Lotus Studio, Bandra West");
        Assert.Null(studio.HomeAddress);
        Assert.Equal("Lotus Studio, Bandra West", studio.StudioAddressSnapshot);

        var onlineSlot = Slot(SessionMode.Online);
        var online = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), ServiceFor(onlineSlot), onlineSlot, 599m, Terms, null, null, "https://meet.google.com/abc-defg-hij", null);
        Assert.Equal("https://meet.google.com/abc-defg-hij", online.MeetLinkSnapshot);

        Assert.Throws<DomainException>(() =>
            BookingRules.CreateAfterPayment(Guid.NewGuid(), ServiceFor(onlineSlot), onlineSlot, 599m, Terms, null, null, null, null));
    }

    [Fact]
    public void Accept_moves_to_upcoming_and_decline_frees_the_slot()
    {
        var booking = Sample(BookingStatus.PendingAccept);
        BookingRules.Accept(booking);
        Assert.Equal(BookingStatus.Upcoming, booking.Status);

        var declined = Sample(BookingStatus.PendingAccept);
        BookingRules.Decline(declined);
        Assert.Equal(BookingStatus.Declined, declined.Status);
        Assert.False(BookingRules.OccupiesSlot(declined.Status));
        Assert.Throws<DomainException>(() => BookingRules.Accept(declined));
    }

    [Fact]
    public void Upcoming_can_complete_no_show_or_cancel_and_reschedule_stays_upcoming()
    {
        var completed = Sample(BookingStatus.Upcoming);
        BookingRules.Complete(completed);
        Assert.Equal(BookingStatus.Completed, completed.Status);

        var noShow = Sample(BookingStatus.Upcoming);
        BookingRules.MarkNoShow(noShow);
        Assert.Equal(BookingStatus.NoShow, noShow.Status);
        Assert.True(BookingRules.OccupiesSlot(noShow.Status));

        var when = DateTimeOffset.UtcNow;
        var cancelled = Sample(BookingStatus.Upcoming);
        BookingRules.Cancel(cancelled, when.AddHours(1), when);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.False(BookingRules.OccupiesSlot(cancelled.Status));

        var booking = Sample(BookingStatus.Upcoming);
        var sameMode = Slot(SessionMode.Home);
        sameMode.ProviderId = booking.ProviderId;
        BookingRules.Reschedule(booking, sameMode);
        Assert.Equal(BookingStatus.Upcoming, booking.Status);
        Assert.Equal(sameMode.Id, booking.SlotId);

        var otherMode = Slot(SessionMode.Online);
        otherMode.ProviderId = booking.ProviderId;
        Assert.Throws<DomainException>(() => BookingRules.Reschedule(booking, otherMode));
    }

    [Fact]
    public void Cancel_allows_pending_or_upcoming_only_before_the_session_starts()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var pending = Sample(BookingStatus.PendingAccept);
        BookingRules.Cancel(pending, start, start.AddMinutes(-1));
        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.False(BookingRules.OccupiesSlot(pending.Status));

        var started = Sample(BookingStatus.Upcoming);
        var tooLate = Assert.Throws<DomainException>(() => BookingRules.Cancel(started, start, start));
        Assert.Contains("started", tooLate.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.Upcoming, started.Status);

        foreach (var status in new[] { BookingStatus.Declined, BookingStatus.Completed, BookingStatus.NoShow, BookingStatus.Cancelled })
        {
            var booking = Sample(status);
            Assert.Throws<DomainException>(() => BookingRules.Cancel(booking, start, start.AddHours(-1)));
            Assert.Equal(status, booking.Status);
        }
    }

    [Fact]
    public void Review_unlocks_after_complete_and_payout_unlocks_after_complete_or_no_show()
    {
        var upcoming = Sample(BookingStatus.Upcoming);
        Assert.Throws<DomainException>(() => ReviewRules.Create(upcoming, upcoming.CustomerId, 5, "Great"));
        Assert.Throws<DomainException>(() => PayoutCalculator.ForCompletedBooking(upcoming));

        BookingRules.Complete(upcoming);
        var review = ReviewRules.Create(upcoming, upcoming.CustomerId, 5, "Calm and clear");
        Assert.Equal(5, review.Rating);

        var payout = PayoutCalculator.ForCompletedBooking(upcoming);
        Assert.Equal(899m, payout.GrossAmount);
        Assert.Equal(134.85m, payout.FeeAmount);
        Assert.Equal(764.15m, payout.NetAmount);
        Assert.Equal(PayoutStatus.Pending, payout.Status);

        var missed = Sample(BookingStatus.Upcoming);
        BookingRules.MarkNoShow(missed);
        Assert.Throws<DomainException>(() => ReviewRules.Create(missed, missed.CustomerId, 5, "Late"));
        var noShowPayout = PayoutCalculator.ForCompletedBooking(missed);
        Assert.Equal(899m, noShowPayout.GrossAmount);
        Assert.Equal(764.15m, noShowPayout.NetAmount);
    }

    [Fact]
    public void Refund_requires_a_captured_payment()
    {
        var payment = new Payment { Status = PaymentStatus.Pending, Amount = 899m };
        Assert.Throws<DomainException>(() => PaymentRules.MarkRefunded(payment));
        PaymentRules.MarkPaid(payment, "pay_test");
        PaymentRules.MarkRefunded(payment);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Partial_refund_keeps_the_rest_and_zero_leaves_the_payment_paid()
    {
        var payment = new Payment { Status = PaymentStatus.Paid, Amount = 899m };
        Assert.Throws<DomainException>(() => PaymentRules.Refund(payment, 900m));
        Assert.Throws<DomainException>(() => PaymentRules.Refund(payment, -1m));

        PaymentRules.Refund(payment, 0m);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(0m, payment.RefundedAmount);

        PaymentRules.Refund(payment, 449.50m);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(449.50m, payment.RefundedAmount);
        Assert.Throws<DomainException>(() => PaymentRules.Refund(payment, 1m));
    }

    [Fact]
    public void Booking_copies_the_terms_it_was_created_with()
    {
        var slot = Slot(SessionMode.Online);
        var terms = new BookingTerms(18m, 29m, 24, LateCancelFeeType.Flat, 200m);
        var booking = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), ServiceFor(slot), slot, 599m, terms, null, null, "https://meet.google.com/abc-defg-hij", null);

        Assert.Equal(18m, booking.CommissionPercent);
        Assert.Equal(29m, booking.ConvenienceFee);
        Assert.Equal(24, booking.CancelFreeWindowHours);
        Assert.Equal(LateCancelFeeType.Flat, booking.LateCancelFeeType);
        Assert.Equal(200m, booking.LateCancelFeeValue);
    }

    [Fact]
    public void Late_cancel_fee_applies_only_to_upcoming_inside_the_window()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var upcoming = Sample(BookingStatus.Upcoming);
        Assert.Equal(0m, BookingRules.LateCancelFeeFor(upcoming, start, start.AddHours(-12)));
        Assert.Equal(449.50m, BookingRules.LateCancelFeeFor(upcoming, start, start.AddHours(-12).AddTicks(1)));
        Assert.Equal(449.50m, BookingRules.LateCancelFeeFor(upcoming, start, start.AddHours(-11)));

        var pending = Sample(BookingStatus.PendingAccept);
        Assert.Equal(0m, BookingRules.LateCancelFeeFor(pending, start, start.AddHours(-1)));
        Assert.Equal(start.AddHours(-12), BookingRules.FreeCancelUntil(upcoming, start));
    }

    [Fact]
    public void Zero_hour_window_never_charges_before_the_session_starts()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var booking = Sample(BookingStatus.Upcoming, window: 0);
        Assert.False(BookingRules.IsLateCancel(booking, start, start.AddTicks(-1)));
        Assert.Equal(0m, BookingRules.LateCancelFeeFor(booking, start, start.AddTicks(-1)));
        Assert.Equal(start, BookingRules.FreeCancelUntil(booking, start));
    }

    [Theory]
    [InlineData(LateCancelFeeType.Percent, 0, 0, 30, 899)]
    [InlineData(LateCancelFeeType.Percent, 100, 899, 30, 0)]
    [InlineData(LateCancelFeeType.Percent, 50, 449.50, 30, 449.50)]
    [InlineData(LateCancelFeeType.Flat, 200, 200, 30, 699)]
    [InlineData(LateCancelFeeType.Flat, 5000, 899, 30, 0)]
    public void Late_cancel_keeps_the_fee_and_the_convenience_fee_and_never_refunds_below_zero(
        LateCancelFeeType type, decimal value, decimal expectedFee, decimal expectedConvenienceKept, decimal expectedRefund)
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var booking = Sample(BookingStatus.Upcoming, feeType: type, feeValue: value, convenienceFee: 30m);
        var paid = booking.Amount + booking.ConvenienceFee;

        var settlement = BookingRules.SettleCustomerCancel(booking, paid, start, start.AddHours(-1));

        Assert.Equal(expectedFee, settlement.LateCancelFee);
        Assert.Equal(expectedConvenienceKept, settlement.ConvenienceFeeKept);
        Assert.Equal(expectedRefund, settlement.Refund);
        Assert.True(settlement.Refund >= 0);
        Assert.Equal(paid, settlement.LateCancelFee + settlement.ConvenienceFeeKept + settlement.Refund);
    }

    [Fact]
    public void Free_cancel_refunds_everything_including_the_convenience_fee()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var booking = Sample(BookingStatus.Upcoming, convenienceFee: 30m);

        var early = BookingRules.SettleCustomerCancel(booking, 929m, start, start.AddHours(-13));
        Assert.Equal(new CancelSettlement(0m, 0m, 929m), early);

        var pending = Sample(BookingStatus.PendingAccept, convenienceFee: 30m);
        Assert.Equal(new CancelSettlement(0m, 0m, 929m), BookingRules.SettleCustomerCancel(pending, 929m, start, start.AddHours(-1)));
    }

    [Fact]
    public void Old_bookings_without_a_convenience_fee_refund_against_what_was_paid()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var booking = Sample(BookingStatus.Upcoming, feeType: LateCancelFeeType.Percent, feeValue: 100m, convenienceFee: 30m);

        var settlement = BookingRules.SettleCustomerCancel(booking, 899m, start, start.AddHours(-1));

        Assert.Equal(899m, settlement.LateCancelFee);
        Assert.Equal(0m, settlement.ConvenienceFeeKept);
        Assert.Equal(0m, settlement.Refund);
    }

    [Fact]
    public void Payout_uses_the_commission_on_the_booking()
    {
        var booking = Sample(BookingStatus.Upcoming);
        booking.CommissionPercent = 0m;
        BookingRules.Complete(booking);
        Assert.Equal(899m, PayoutCalculator.ForCompletedBooking(booking).NetAmount);

        var full = Sample(BookingStatus.Upcoming);
        full.CommissionPercent = 100m;
        BookingRules.Complete(full);
        Assert.Equal(0m, PayoutCalculator.ForCompletedBooking(full).NetAmount);
    }

    [Fact]
    public void Late_cancel_payout_uses_the_kept_amount()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        var booking = Sample(BookingStatus.Upcoming);
        Assert.Throws<DomainException>(() => PayoutCalculator.ForLateCancel(booking, 449.50m));

        BookingRules.Cancel(booking, start, start.AddHours(-1));
        Assert.Equal(CancelledBy.Customer, booking.CancelledBy);
        Assert.Throws<DomainException>(() => PayoutCalculator.ForLateCancel(booking, 0m));
        Assert.Throws<DomainException>(() => PayoutCalculator.ForLateCancel(booking, 900m));

        var payout = PayoutCalculator.ForLateCancel(booking, 449.50m);
        Assert.Equal(449.50m, payout.GrossAmount);
        Assert.Equal(67.43m, payout.FeeAmount);
        Assert.Equal(382.07m, payout.NetAmount);
    }

    [Fact]
    public void Admin_cancel_needs_a_reason_and_a_live_booking()
    {
        var now = DateTimeOffset.UtcNow;
        var booking = Sample(BookingStatus.Upcoming);
        Assert.Throws<DomainException>(() => BookingRules.AdminCancel(booking, " no ", now));
        Assert.Throws<DomainException>(() => BookingRules.AdminCancel(booking, new string('x', 301), now));
        Assert.Equal(BookingStatus.Upcoming, booking.Status);

        BookingRules.AdminCancel(booking, "  Instructor is unwell  ", now);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(CancelledBy.Admin, booking.CancelledBy);
        Assert.Equal("Instructor is unwell", booking.CancelReason);

        foreach (var status in new[] { BookingStatus.Declined, BookingStatus.Completed, BookingStatus.NoShow, BookingStatus.Cancelled })
            Assert.Throws<DomainException>(() => BookingRules.AdminCancel(Sample(status), "Duplicate booking", now));
    }

    [Fact]
    public void Free_window_uses_the_configured_hours()
    {
        var start = new DateTimeOffset(2026, 9, 24, 1, 30, 0, TimeSpan.Zero);
        Assert.True(BookingRules.IsFreeWindow(start, start.AddHours(-12), 12));
        Assert.False(BookingRules.IsFreeWindow(start, start.AddHours(-11), 12));
    }

    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("919876543210", "+919876543210")]
    public void Phone_normalizes_indian_mobiles(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("5876543210")]
    [InlineData("")]
    public void Phone_rejects_invalid_numbers(string input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static readonly BookingTerms Terms = new(15m, 0m, 12, LateCancelFeeType.Percent, 50m);

    private static Booking Sample(
        BookingStatus status,
        int window = 12,
        LateCancelFeeType feeType = LateCancelFeeType.Percent,
        decimal feeValue = 50m,
        decimal convenienceFee = 0m) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        ProviderId = Guid.NewGuid(),
        ServiceId = Guid.NewGuid(),
        SlotId = Guid.NewGuid(),
        Mode = SessionMode.Home,
        Status = status,
        Amount = 899m,
        CommissionPercent = 15m,
        ConvenienceFee = convenienceFee,
        CancelFreeWindowHours = window,
        LateCancelFeeType = feeType,
        LateCancelFeeValue = feeValue,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static AvailabilitySlot Slot(SessionMode mode) => new()
    {
        Id = Guid.NewGuid(),
        ProviderId = Guid.NewGuid(),
        Mode = mode,
        Date = new DateOnly(2026, 9, 24),
        StartTime = new TimeOnly(7, 0),
        EndTime = new TimeOnly(8, 0)
    };

    private static Service ServiceFor(AvailabilitySlot slot) => new()
    {
        Id = Guid.NewGuid(),
        ProviderId = slot.ProviderId,
        CategoryId = Guid.NewGuid(),
        Title = "Yoga session",
        IsActive = true
    };
}

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
            BookingRules.CreateAfterPayment(Guid.NewGuid(), service, slot, 899m, "Bandra West", null, null, null));
        Assert.Contains("landmark", missing.Message, StringComparison.OrdinalIgnoreCase);

        var booking = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), service, slot, 899m, "14th Road, Bandra West", "Near the station", null, null);

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
            Guid.NewGuid(), ServiceFor(studioSlot), studioSlot, 749m, null, null, null, "Lotus Studio, Bandra West");
        Assert.Null(studio.HomeAddress);
        Assert.Equal("Lotus Studio, Bandra West", studio.StudioAddressSnapshot);

        var onlineSlot = Slot(SessionMode.Online);
        var online = BookingRules.CreateAfterPayment(
            Guid.NewGuid(), ServiceFor(onlineSlot), onlineSlot, 599m, null, null, "https://meet.google.com/abc-defg-hij", null);
        Assert.Equal("https://meet.google.com/abc-defg-hij", online.MeetLinkSnapshot);

        Assert.Throws<DomainException>(() =>
            BookingRules.CreateAfterPayment(Guid.NewGuid(), ServiceFor(onlineSlot), onlineSlot, 599m, null, null, null, null));
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
    public void Review_and_payout_unlock_only_after_complete()
    {
        var upcoming = Sample(BookingStatus.Upcoming);
        Assert.Throws<DomainException>(() => ReviewRules.Create(upcoming, upcoming.CustomerId, 5, "Great"));
        Assert.Throws<DomainException>(() => PayoutCalculator.ForCompletedBooking(upcoming, 15m));

        BookingRules.Complete(upcoming);
        var review = ReviewRules.Create(upcoming, upcoming.CustomerId, 5, "Calm and clear");
        Assert.Equal(5, review.Rating);

        var payout = PayoutCalculator.ForCompletedBooking(upcoming, 15m);
        Assert.Equal(899m, payout.GrossAmount);
        Assert.Equal(134.85m, payout.FeeAmount);
        Assert.Equal(764.15m, payout.NetAmount);
        Assert.Equal(PayoutStatus.Pending, payout.Status);
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

    private static Booking Sample(BookingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        ProviderId = Guid.NewGuid(),
        ServiceId = Guid.NewGuid(),
        SlotId = Guid.NewGuid(),
        Mode = SessionMode.Home,
        Status = status,
        Amount = 899m,
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

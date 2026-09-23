namespace YogaMarketplace.Domain;

/// <summary>
/// Booking handshake. Pay-at-book creates PendingAccept.
/// Decline refunds (the caller) and frees the slot. Complete unlocks a review and a pending payout.
/// Cancel and reschedule windows live on <see cref="MarketplacePolicy"/> as TBD defaults and are not enforced here.
/// </summary>
public static class BookingRules
{
    public static bool OccupiesSlot(BookingStatus status) =>
        status is BookingStatus.PendingAccept
            or BookingStatus.Upcoming
            or BookingStatus.Completed
            or BookingStatus.NoShow;

    public static Booking CreateAfterPayment(
        Guid customerId,
        Service service,
        AvailabilitySlot slot,
        decimal amount,
        string? homeAddress,
        string? landmark,
        string? meetLink,
        string? studioAddress)
    {
        if (service.ProviderId != slot.ProviderId)
            throw new DomainException("Service and slot must belong to the same instructor.");
        if (slot.IsBlocked)
            throw new DomainException("That slot is blocked.");
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");

        EnsureSessionLocation(slot.Mode, homeAddress, landmark, meetLink, studioAddress);

        var now = DateTimeOffset.UtcNow;
        return new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProviderId = slot.ProviderId,
            ServiceId = service.Id,
            SlotId = slot.Id,
            Mode = slot.Mode,
            Status = BookingStatus.PendingAccept,
            Amount = amount,
            HomeAddress = slot.Mode == SessionMode.Home ? homeAddress!.Trim() : null,
            Landmark = slot.Mode == SessionMode.Home ? landmark!.Trim() : null,
            MeetLinkSnapshot = slot.Mode == SessionMode.Online ? meetLink!.Trim() : null,
            StudioAddressSnapshot = slot.Mode == SessionMode.Studio ? studioAddress!.Trim() : null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static void EnsureSessionLocation(
        SessionMode mode,
        string? homeAddress,
        string? landmark,
        string? meetLink,
        string? studioAddress)
    {
        if (mode == SessionMode.Home && (string.IsNullOrWhiteSpace(homeAddress) || string.IsNullOrWhiteSpace(landmark)))
            throw new DomainException("Home sessions require an address and a landmark.");
        if (mode == SessionMode.Online && string.IsNullOrWhiteSpace(meetLink))
            throw new DomainException("Online sessions require a Google Meet link.");
        if (mode == SessionMode.Studio && string.IsNullOrWhiteSpace(studioAddress))
            throw new DomainException("Studio sessions require the studio address.");
    }

    public static void Accept(Booking booking)
    {
        if (booking.Status != BookingStatus.PendingAccept)
            throw new DomainException("Only a pending booking can be accepted.");
        booking.Status = BookingStatus.Upcoming;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void Decline(Booking booking)
    {
        if (booking.Status != BookingStatus.PendingAccept)
            throw new DomainException("Only a pending booking can be declined.");
        booking.Status = BookingStatus.Declined;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void Cancel(Booking booking)
    {
        if (booking.Status != BookingStatus.Upcoming)
            throw new DomainException("Only an upcoming booking can be cancelled.");
        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void MarkNoShow(Booking booking)
    {
        if (booking.Status != BookingStatus.Upcoming)
            throw new DomainException("Only an upcoming booking can be marked as a no-show.");
        booking.Status = BookingStatus.NoShow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void Complete(Booking booking)
    {
        if (booking.Status != BookingStatus.Upcoming)
            throw new DomainException("Only an upcoming booking can be completed.");
        booking.Status = BookingStatus.Completed;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void Reschedule(Booking booking, AvailabilitySlot newSlot)
    {
        if (booking.Status != BookingStatus.Upcoming)
            throw new DomainException("Only an upcoming booking can be rescheduled.");
        if (newSlot.Id == booking.SlotId)
            throw new DomainException("Pick a different slot.");
        if (newSlot.ProviderId != booking.ProviderId)
            throw new DomainException("Reschedule must stay with the same instructor.");
        if (newSlot.Mode != booking.Mode)
            throw new DomainException("Reschedule must stay on the same session mode.");
        if (newSlot.IsBlocked)
            throw new DomainException("That slot is blocked.");

        booking.SlotId = newSlot.Id;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static bool IsFreeWindow(DateTimeOffset sessionStart, DateTimeOffset now, int freeWindowHours) =>
        sessionStart - now >= TimeSpan.FromHours(freeWindowHours);
}

public static class PaymentRules
{
    public static void MarkPaid(Payment payment, string? gatewayPaymentId)
    {
        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
            throw new DomainException("This payment can no longer be captured.");
        payment.Status = PaymentStatus.Paid;
        payment.GatewayPaymentId = gatewayPaymentId;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void MarkRefunded(Payment payment)
    {
        if (payment.Status != PaymentStatus.Paid)
            throw new DomainException("Only a captured payment can be refunded.");
        payment.Status = PaymentStatus.Refunded;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public static class ReviewRules
{
    public static Review Create(Booking booking, Guid customerId, int rating, string? comment)
    {
        if (booking.Status != BookingStatus.Completed)
            throw new DomainException("Reviews are available only after the session is completed.");
        if (booking.CustomerId != customerId)
            throw new DomainException("Only the customer who booked can review.");
        if (rating is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5.");
        if (comment is { Length: > 1000 })
            throw new DomainException("Keep the review under 1000 characters.");

        return new Review
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CustomerId = customerId,
            ProviderId = booking.ProviderId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

public static class PayoutCalculator
{
    public static PayoutPending ForCompletedBooking(Booking booking, decimal feePercent)
    {
        if (booking.Status != BookingStatus.Completed)
            throw new DomainException("A payout is created when a booking is completed.");
        if (feePercent is < 0 or > 100)
            throw new DomainException("Platform fee percent must be between 0 and 100.");

        var fee = Math.Round(booking.Amount * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new PayoutPending
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            ProviderId = booking.ProviderId,
            GrossAmount = booking.Amount,
            FeePercent = feePercent,
            FeeAmount = fee,
            NetAmount = booking.Amount - fee,
            Status = PayoutStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

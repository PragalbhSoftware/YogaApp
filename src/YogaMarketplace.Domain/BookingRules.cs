namespace YogaMarketplace.Domain;

/// <summary>
/// Booking handshake. Pay-at-book creates PendingAccept.
/// Decline refunds (the caller) and frees the slot. Complete unlocks a review and a pending payout.
/// No-show keeps the slot occupied, creates the same pending payout, and does not unlock a review.
/// Cancel allows PendingAccept or Upcoming before the session starts and frees the slot.
/// Fees, windows and commission come from the terms copied onto the booking at creation, never from current settings.
/// Cancelling an Upcoming booking inside its free window keeps <see cref="LateCancelFeeFor"/> and the convenience fee;
/// the rest is refunded and the kept late fee becomes an instructor payout. PendingAccept cancels are always free.
/// Admin force-cancel allows PendingAccept or Upcoming at any time, needs a reason, and always refunds in full.
/// Reschedule keeps one Upcoming booking on another slot with the same instructor and mode. It is never charged,
/// so it is refused inside the late-cancel window; otherwise moving a late booking would reset the fee.
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
        BookingTerms terms,
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
            CommissionPercent = terms.CommissionPercent,
            ConvenienceFee = terms.ConvenienceFee,
            CancelFreeWindowHours = terms.CancelFreeWindowHours,
            LateCancelFeeType = terms.LateCancelFeeType,
            LateCancelFeeValue = terms.LateCancelFeeValue,
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

    public static void Cancel(Booking booking, DateTimeOffset sessionStart, DateTimeOffset now)
    {
        if (booking.Status is not (BookingStatus.PendingAccept or BookingStatus.Upcoming))
            throw new DomainException("Only a pending or upcoming booking can be cancelled.");
        if (now >= sessionStart)
            throw new DomainException("This session has already started.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledBy = CancelledBy.Customer;
        booking.UpdatedAt = now;
    }

    public const int CancelReasonMin = 5;
    public const int CancelReasonMax = 300;

    public static void AdminCancel(Booking booking, string? reason, DateTimeOffset now)
    {
        if (booking.Status is not (BookingStatus.PendingAccept or BookingStatus.Upcoming))
            throw new DomainException("Only a pending or upcoming booking can be cancelled.");
        var trimmed = (reason ?? "").Trim();
        if (trimmed.Length is < CancelReasonMin or > CancelReasonMax)
            throw new DomainException($"Give a reason of {CancelReasonMin} to {CancelReasonMax} characters.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledBy = CancelledBy.Admin;
        booking.CancelReason = trimmed;
        booking.UpdatedAt = now;
    }

    public static DateTimeOffset FreeCancelUntil(Booking booking, DateTimeOffset sessionStart) =>
        sessionStart - TimeSpan.FromHours(booking.CancelFreeWindowHours);

    /// <summary>True for an accepted booking cancelled inside its free window.</summary>
    public static bool IsLateCancel(Booking booking, DateTimeOffset sessionStart, DateTimeOffset now) =>
        booking.Status == BookingStatus.Upcoming && !IsFreeWindow(sessionStart, now, booking.CancelFreeWindowHours);

    /// <summary>Late fee kept from the session amount if the customer cancels now. Never more than the session amount.</summary>
    public static decimal LateCancelFeeFor(Booking booking, DateTimeOffset sessionStart, DateTimeOffset now)
    {
        if (!IsLateCancel(booking, sessionStart, now))
            return 0m;
        var fee = booking.LateCancelFeeType == LateCancelFeeType.Flat
            ? booking.LateCancelFeeValue
            : Math.Round(booking.Amount * booking.LateCancelFeeValue / 100m, 2, MidpointRounding.AwayFromZero);
        return Math.Clamp(fee, 0m, booking.Amount);
    }

    /// <summary>
    /// Splits <paramref name="paid"/> for a customer cancel. A late cancel keeps the late fee and the convenience fee;
    /// anything else refunds everything. The refund is never negative.
    /// </summary>
    public static CancelSettlement SettleCustomerCancel(Booking booking, decimal paid, DateTimeOffset sessionStart, DateTimeOffset now)
    {
        var lateFee = Math.Min(LateCancelFeeFor(booking, sessionStart, now), paid);
        var convenienceKept = IsLateCancel(booking, sessionStart, now)
            ? Math.Clamp(booking.ConvenienceFee, 0m, paid - lateFee)
            : 0m;
        return new CancelSettlement(lateFee, convenienceKept, paid - lateFee - convenienceKept);
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

    public static void EnsureOutsideLateWindow(Booking booking, DateTimeOffset sessionStart, DateTimeOffset now)
    {
        if (!IsFreeWindow(sessionStart, now, booking.CancelFreeWindowHours))
            throw new DomainException("This session is too close to move. You can still cancel it.", 409);
    }

    public static bool IsFreeWindow(DateTimeOffset sessionStart, DateTimeOffset now, int freeWindowHours) =>
        sessionStart - now >= TimeSpan.FromHours(freeWindowHours);
}

public sealed record CancelSettlement(decimal LateCancelFee, decimal ConvenienceFeeKept, decimal Refund);

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

    public static void MarkRefunded(Payment payment) => Refund(payment, payment.Amount);

    /// <summary>Records a refund of <paramref name="amount"/>. Zero leaves the payment Paid.</summary>
    public static void Refund(Payment payment, decimal amount)
    {
        if (payment.Status != PaymentStatus.Paid)
            throw new DomainException("Only a captured payment can be refunded.");
        if (amount < 0 || amount > payment.Amount)
            throw new DomainException("Refund must be between zero and the amount paid.");
        if (amount == 0)
            return;

        payment.RefundedAmount = amount;
        payment.Status = amount == payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
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

/// <summary>Instructor payouts use the commission copied onto the booking. The convenience fee is never paid out.</summary>
public static class PayoutCalculator
{
    public static PayoutPending ForCompletedBooking(Booking booking)
    {
        if (booking.Status is not (BookingStatus.Completed or BookingStatus.NoShow))
            throw new DomainException("A payout is created when a session is completed or marked no-show.");
        return Build(booking, booking.Amount);
    }

    public static PayoutPending ForLateCancel(Booking booking, decimal keptAmount)
    {
        if (booking.Status != BookingStatus.Cancelled || booking.CancelledBy != CancelledBy.Customer)
            throw new DomainException("A late-cancel payout needs a customer-cancelled booking.");
        if (keptAmount <= 0 || keptAmount > booking.Amount)
            throw new DomainException("Kept amount must be above zero and at most the booking amount.");
        return Build(booking, keptAmount);
    }

    private static PayoutPending Build(Booking booking, decimal gross)
    {
        var feePercent = booking.CommissionPercent;
        if (feePercent is < 0 or > 100)
            throw new DomainException("Platform fee percent must be between 0 and 100.");

        var fee = Math.Round(gross * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new PayoutPending
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            ProviderId = booking.ProviderId,
            GrossAmount = gross,
            FeePercent = feePercent,
            FeeAmount = fee,
            NetAmount = gross - fee,
            Status = PayoutStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

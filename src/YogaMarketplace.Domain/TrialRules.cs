namespace YogaMarketplace.Domain;

/// <summary>
/// A trial is one discounted or free session per customer per instructor, offered on one of the instructor's services.
/// It is used up by any trial booking except one the instructor declined or admin cancelled; a customer cancel or a
/// no-show does not give it back. A free trial still pays the convenience fee through Razorpay, so the total must reach
/// the gateway minimum.
/// </summary>
public static class TrialRules
{
    /// <summary>Razorpay's smallest order.</summary>
    public const decimal MinimumCharge = 1m;

    public const string AlreadyUsedMessage = "You have already had your trial class with this instructor.";
    public const string NotOfferedMessage = "This instructor doesn't offer a trial class right now.";
    public const string BelowMinimumMessage =
        "This trial can't be booked right now. Ask the instructor to set a price of at least ₹1.";

    /// <summary>True when this booking uses up the customer's trial with the instructor.</summary>
    public static bool Consumes(Booking booking) =>
        booking.IsTrial
        && booking.Status != BookingStatus.Declined
        && !(booking.Status == BookingStatus.Cancelled && booking.CancelledBy == CancelledBy.Admin);

    /// <summary>
    /// Stores <see cref="Consumes"/> on the booking. Call it wherever the status changes so the flag is saved with the
    /// status; the database keeps at most one consumed trial per customer and instructor.
    /// </summary>
    public static void Sync(Booking booking) => booking.TrialConsumed = Consumes(booking);

    public static bool Offers(Service service) =>
        service.IsActive && service.TrialEnabled && service.TrialPrice is not null;

    /// <summary>
    /// Validates and rounds a trial price. It may be zero, but never above the instructor's lowest regular fee,
    /// and with the current convenience fee the total must reach <see cref="MinimumCharge"/>.
    /// </summary>
    public static decimal CleanPrice(decimal price, decimal? lowestRegularRate, decimal convenienceFee)
    {
        var rounded = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        if (rounded < 0)
            throw new DomainException("Trial price can't be negative.");
        if (lowestRegularRate is null)
            throw new DomainException("Set a regular session fee before offering a trial.");
        if (rounded > lowestRegularRate)
            throw new DomainException($"Keep the trial price at or below your lowest fee of ₹{lowestRegularRate:0.##}.");
        if (rounded + convenienceFee < MinimumCharge)
            throw new DomainException($"Set a trial price of at least ₹{MinimumCharge - convenienceFee:0.##} while the convenience fee is ₹{convenienceFee:0.##}.");
        return rounded;
    }

    public static void Enable(Service service, decimal cleanPrice)
    {
        service.TrialEnabled = true;
        service.TrialPrice = cleanPrice;
    }

    public static void Disable(Service service) => service.TrialEnabled = false;

    public static void EnsureChargeable(decimal total)
    {
        if (total < MinimumCharge)
            throw new DomainException(BelowMinimumMessage, 409);
    }
}

namespace YogaMarketplace.Domain;

/// <summary>Event keys. Customer and instructor get different text, so each audience has its own key.</summary>
public static class NotificationEvents
{
    public const string BookingRequestedCustomer = "booking.requested.customer";
    public const string BookingRequestedInstructor = "booking.requested.instructor";
    public const string BookingConfirmedCustomer = "booking.confirmed.customer";
    public const string BookingConfirmedInstructor = "booking.confirmed.instructor";
    public const string BookingCancelledCustomer = "booking.cancelled.customer";
    public const string BookingCancelledInstructor = "booking.cancelled.instructor";
    public const string BookingRefundedCustomer = "booking.refunded.customer";
    public const string BookingRefundedInstructor = "booking.refunded.instructor";
    public const string PayoutPaidInstructor = "payout.paid.instructor";
}

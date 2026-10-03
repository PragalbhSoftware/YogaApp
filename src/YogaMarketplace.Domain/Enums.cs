namespace YogaMarketplace.Domain;

public enum UserRole
{
    Customer,
    Provider,
    Admin
}

public enum Gender
{
    Female,
    Male,
    Other
}

public enum ProviderStatus
{
    Pending,
    Verified,
    Rejected
}

public enum SessionMode
{
    Home,
    Studio,
    Online
}

public enum BookingStatus
{
    PendingAccept,
    Upcoming,
    Declined,
    Completed,
    NoShow,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded
}

public enum UserBlockAction
{
    Blocked,
    Unblocked
}

public enum RefreshTokenRevokeReason
{
    Rotated,
    Reused,
    SignedOut,
    Blocked
}

public enum CancelledBy
{
    Customer,
    Admin
}

/// <summary>
/// Unpaid Razorpay checkout. A booking row is created only after capture.
/// </summary>
public enum CheckoutStatus
{
    Open,
    Completed
}

public enum PayoutStatus
{
    Pending,
    Exported,
    Paid
}

public enum LateCancelFeeType
{
    Percent,
    Flat
}

public enum PayoutCycle
{
    Weekly,
    Biweekly
}

public enum BackgroundJobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public static class PaymentGateways
{
    public const string Razorpay = "razorpay";
}

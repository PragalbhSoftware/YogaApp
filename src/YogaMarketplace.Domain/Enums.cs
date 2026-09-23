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
    Refunded
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

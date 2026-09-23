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

public enum PayoutStatus
{
    Pending,
    Exported,
    Paid
}

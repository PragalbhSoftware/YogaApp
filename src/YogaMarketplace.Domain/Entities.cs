namespace YogaMarketplace.Domain;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Area
{
    public Guid Id { get; set; }
    public string City { get; set; } = "Mumbai";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class User
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public Gender? Gender { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public DateTimeOffset CreatedAt { get; set; }
    public Provider? Provider { get; set; }
}

public class Provider
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public ProviderStatus Status { get; set; } = ProviderStatus.Pending;
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public int? Age { get; set; }
    public Guid AreaId { get; set; }
    public Area? Area { get; set; }
    public bool OffersHome { get; set; }
    public bool OffersStudio { get; set; }
    public bool OffersOnline { get; set; }
    public decimal? HomeRate { get; set; }
    public decimal? StudioRate { get; set; }
    public decimal? OnlineRate { get; set; }
    public string? StudioAddress { get; set; }
    public string? GoogleMeetLink { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public List<Service> Services { get; set; } = new();
    public List<AvailabilitySlot> Slots { get; set; } = new();

    public bool Offers(SessionMode mode) => mode switch
    {
        SessionMode.Home => OffersHome,
        SessionMode.Studio => OffersStudio,
        SessionMode.Online => OffersOnline,
        _ => false
    };

    public decimal? RateFor(SessionMode mode) => mode switch
    {
        SessionMode.Home => HomeRate,
        SessionMode.Studio => StudioRate,
        SessionMode.Online => OnlineRate,
        _ => null
    };
}

public class Service
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Title { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class AvailabilitySlot
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public SessionMode Mode { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsBlocked { get; set; }
}

public class Booking
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public User? Customer { get; set; }
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public Guid ServiceId { get; set; }
    public Service? Service { get; set; }
    public Guid SlotId { get; set; }
    public AvailabilitySlot? Slot { get; set; }
    public SessionMode Mode { get; set; }
    public BookingStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string? HomeAddress { get; set; }
    public string? Landmark { get; set; }
    public string? MeetLinkSnapshot { get; set; }
    public string? StudioAddressSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Payment? Payment { get; set; }
    public Review? Review { get; set; }
    public PayoutPending? Payout { get; set; }
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Gateway { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProviderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PayoutPending
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid ProviderId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeePercent { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}

public class MarketplacePolicy
{
    public Guid Id { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal PlatformFeePercent { get; set; }
    public int CancelFreeWindowHours { get; set; }
    public int RescheduleFreeWindowHours { get; set; }
    public decimal LateCancelFeePercent { get; set; }
    public string PolicyNote { get; set; } = "";
}

public class OtpChallenge
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
    public bool IsNewUser { get; set; }
    public string? PendingName { get; set; }
    public Gender? PendingGender { get; set; }
    public UserRole IntendedRole { get; set; } = UserRole.Customer;
    public DateTimeOffset CreatedAt { get; set; }
}

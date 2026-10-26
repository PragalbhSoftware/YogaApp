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
    public Guid CityId { get; set; }
    public City? City { get; set; }
    public string Name { get; set; } = "";
    public string Pincode { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
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
    public string? HomeLine1 { get; set; }
    public string? HomeArea { get; set; }
    public string? HomeCity { get; set; }
    public string? HomePin { get; set; }
    public string? HomeLandmark { get; set; }
    public bool IsBlocked { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }
    public string? BlockedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Provider? Provider { get; set; }
}

/// <summary>Audit row for every admin block or unblock of a user.</summary>
public class UserBlockEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AdminUserId { get; set; }
    public UserBlockAction Action { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// One link in a refresh-token chain. Only the SHA-256 hash of the token is stored.
/// Every token issued from one sign-in shares a <see cref="FamilyId"/>.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public RefreshTokenRevokeReason? RevokeReason { get; set; }
    public Guid? ReplacedById { get; set; }
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
    /// <summary>YouTube video id only (11 characters). Shown through youtube-nocookie.com embeds.</summary>
    public string? IntroVideoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public List<Service> Services { get; set; } = new();
    public List<AvailabilitySlot> Slots { get; set; } = new();
    public List<ProviderPhoto> Photos { get; set; } = new();
    public List<ProviderCertificate> Certificates { get; set; } = new();

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

    public decimal? LowestOfferedRate() =>
        Enum.GetValues<SessionMode>()
            .Where(Offers)
            .Select(RateFor)
            .Where(rate => rate is > 0)
            .Min();
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

    /// <summary>Offers one trial session per customer at <see cref="TrialPrice"/>. See <see cref="TrialRules"/>.</summary>
    public bool TrialEnabled { get; set; }

    public decimal? TrialPrice { get; set; }
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
    public bool IsTrial { get; set; }
    /// <summary>Stored result of <see cref="TrialRules.Consumes"/>; a unique filtered index allows one per customer and instructor.</summary>
    public bool TrialConsumed { get; set; }
    public string? HomeAddress { get; set; }
    public string? Landmark { get; set; }
    public string? MeetLinkSnapshot { get; set; }
    public string? StudioAddressSnapshot { get; set; }
    public CancelledBy? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
    public decimal? LateCancelFee { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal ConvenienceFee { get; set; }
    public int CancelFreeWindowHours { get; set; }
    public LateCancelFeeType LateCancelFeeType { get; set; } = LateCancelFeeType.Percent;
    public decimal LateCancelFeeValue { get; set; }
    /// <summary>Set when the session was paid with a credit. <see cref="Amount"/> then holds the frozen instructor gross.</summary>
    public Guid? CreditBatchId { get; set; }

    /// <summary>The credit's frozen worth: pack price divided by its sessions.</summary>
    public decimal? CreditValue { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Payment? Payment { get; set; }
    public Review? Review { get; set; }
    public PayoutPending? Payout { get; set; }
}

/// <summary>
/// Holds a Razorpay order until payment is captured. Not a booking.
/// Abandoning checkout leaves this row and creates no <see cref="Booking"/>.
/// The booking terms are locked here when checkout starts, so a settings change before capture does not apply.
/// A <see cref="CheckoutKind.Pack"/> checkout buys <see cref="PackId"/> and has no provider, service or slot.
/// </summary>
public class CheckoutIntent
{
    public Guid Id { get; set; }
    public CheckoutKind Kind { get; set; } = CheckoutKind.Booking;
    public Guid CustomerId { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid? PackId { get; set; }

    /// <summary>The pack as ordered, so a pack edited before capture still gives what was paid for. Null on older orders.</summary>
    public int? PackSessions { get; set; }
    public int? PackValidityDays { get; set; }
    public decimal? PackPriceCap { get; set; }

    /// <summary>A booking paid with a credit; the order charges only the convenience fee.</summary>
    public bool UsesCredit { get; set; }
    public SessionMode Mode { get; set; }
    public decimal Amount { get; set; }
    public bool IsTrial { get; set; }
    public decimal ConvenienceFee { get; set; }
    public decimal CommissionPercent { get; set; }
    public int CancelFreeWindowHours { get; set; }
    public LateCancelFeeType LateCancelFeeType { get; set; } = LateCancelFeeType.Percent;
    public decimal LateCancelFeeValue { get; set; }
    public string Currency { get; set; } = "INR";
    public string? HomeAddress { get; set; }
    public string? Landmark { get; set; }
    public string Gateway { get; set; } = PaymentGateways.Razorpay;
    public string GatewayOrderId { get; set; } = "";
    public string? GatewayPaymentId { get; set; }
    public CheckoutStatus Status { get; set; } = CheckoutStatus.Open;
    public Guid? BookingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public BookingTerms ToTerms() =>
        new(CommissionPercent, ConvenienceFee, CancelFreeWindowHours, LateCancelFeeType, LateCancelFeeValue);
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Gateway { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }

    /// <summary>Razorpay's id for the refund on this payment; a refund webhook with this id is already recorded.</summary>
    public string? GatewayRefundId { get; set; }
    public RefundStatus? RefundStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// One gateway webhook delivery, unique per provider and event id, so a redelivery is not processed twice.
/// A refund that arrives before its payment is captured stays <see cref="WebhookEventStatus.Pending"/> and is applied at capture.
/// </summary>
public class WebhookEvent
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "";
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? GatewayPaymentId { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;
    public string? Error { get; set; }
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
    public Guid? ExportBatchId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// The single row of business values the owner edits from admin. Bookings copy the money fields
/// when they are created (see <see cref="BookingTerms"/>), so edits only affect new bookings.
/// </summary>
public class PlatformSettings
{
    public Guid Id { get; set; }
    public string Currency { get; set; } = "INR";
    public string TimeZoneId { get; set; } = MarketClock.DefaultTimeZoneId;
    public decimal CommissionPercent { get; set; }
    public decimal ConvenienceFee { get; set; }
    public int CancelFreeWindowHours { get; set; }
    public int RescheduleFreeWindowHours { get; set; }
    public LateCancelFeeType LateCancelFeeType { get; set; } = LateCancelFeeType.Percent;
    public decimal LateCancelFeeValue { get; set; }
    public PayoutCycle PayoutCycle { get; set; } = PayoutCycle.Weekly;
    public string PolicyNote { get; set; } = "";
    public string? BannerTitle { get; set; }
    public string? BannerSubtitle { get; set; }
    public string? BannerOffer { get; set; }
    public bool CreditConvenienceFee { get; set; }
    public CreditPayBasis CreditPayBasis { get; set; } = CreditPayBasis.PackPerSession;
    public CreditLateCancel CreditLateCancel { get; set; } = CreditLateCancel.PayInstructorShare;
    public bool CreditBookingStandsAfterExpiry { get; set; } = true;
    public bool PackRefundsEnabled { get; set; } = true;
    public decimal PackReferenceSessionPrice { get; set; } = PlatformSettingsRules.DefaultPackReferenceSessionPrice;
    public decimal PackRefundFee { get; set; }

    /// <summary>Market-clock time the daily Razorpay reconciliation runs; it covers the previous market day.</summary>
    public TimeOnly ReconciliationTime { get; set; } = PlatformSettingsRules.DefaultReconciliationTime;
    public int WebhookStuckMinutes { get; set; } = PlatformSettingsRules.DefaultWebhookStuckMinutes;
    public int RefundPendingHours { get; set; } = PlatformSettingsRules.DefaultRefundPendingHours;

    /// <summary>Version of the Terms and Privacy pages users must accept. Raising it asks every user again once.</summary>
    public int PolicyVersion { get; set; } = 1;
    public int WebhookPayloadRetentionDays { get; set; } = PlatformSettingsRules.DefaultWebhookPayloadRetentionDays;

    /// <summary>On/off and market-clock times of the recurring jobs.</summary>
    public bool ReconciliationEnabled { get; set; } = true;
    public bool CreditExpiryEnabled { get; set; } = true;
    public TimeOnly CreditExpiryTime { get; set; } = PlatformSettingsRules.DefaultDailyJobTime;
    public bool WebhookCleanupEnabled { get; set; } = true;
    public TimeOnly WebhookCleanupTime { get; set; } = PlatformSettingsRules.DefaultDailyJobTime;

    /// <summary>Error alerts: the email feature flag, who gets them, how often per error type, and how long errors are kept.</summary>
    public bool EmailAlertsEnabled { get; set; }
    public string? ErrorAlertEmail { get; set; }
    public int ErrorAlertThrottleMinutes { get; set; } = PlatformSettingsRules.DefaultErrorAlertThrottleMinutes;
    public int ErrorAlertCheckMinutes { get; set; } = PlatformSettingsRules.DefaultErrorAlertCheckMinutes;
    public int ErrorEventRetentionDays { get; set; } = PlatformSettingsRules.DefaultErrorEventRetentionDays;

    /// <summary>Customer and instructor notification emails: the feature flag and how many times one send is tried.</summary>
    public bool EmailNotificationsEnabled { get; set; }
    public int NotificationMaxAttempts { get; set; } = PlatformSettingsRules.DefaultNotificationMaxAttempts;

    /// <summary>Per-phone sign-in code limits, see <see cref="OtpLimitTerms"/>.</summary>
    public int OtpMaxSendsPerPhone { get; set; } = PlatformSettingsRules.DefaultOtpMaxSendsPerPhone;
    public int OtpMaxWrongCodesPerPhone { get; set; } = PlatformSettingsRules.DefaultOtpMaxWrongCodesPerPhone;
    public int OtpLockMinutes { get; set; } = PlatformSettingsRules.DefaultOtpLockMinutes;
    public int Version { get; set; } = 1;
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>One changed field from one admin save of <see cref="PlatformSettings"/>.</summary>
public class SettingsAudit
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string Field { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// One row per kind of logged Error or Critical, keyed by <see cref="ErrorType"/>. A new occurrence bumps the count;
/// after an admin acknowledges it, the next occurrence reopens the row with the count starting again at 1.
/// </summary>
public class ErrorEvent
{
    public const int ErrorTypeMax = 200;
    public const int LevelMax = 16;
    public const int SourceMax = 200;
    public const int MessageMax = 2000;
    public const int CorrelationIdMax = 64;

    public Guid Id { get; set; }
    public string ErrorType { get; set; } = "";
    public string Level { get; set; } = "";
    public string? Source { get; set; }
    public string LastMessage { get; set; } = "";
    public string? LastCorrelationId { get; set; }
    public int Count { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public DateTimeOffset? LastAlertedAt { get; set; }
}

/// <summary>A unit of work for the background runner. Handlers are looked up by <see cref="Type"/>.</summary>
public class BackgroundJob
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string? Payload { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? LastError { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Set on the one queued run of a recurring job, unique per type, so two instances never queue it twice.
    /// Cleared when that run succeeds or fails for good.
    /// </summary>
    public string? RecurringType { get; set; }
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
    public bool ConsentAccepted { get; set; }
    public UserRole IntendedRole { get; set; } = UserRole.Customer;
    public DateTimeOffset CreatedAt { get; set; }
}

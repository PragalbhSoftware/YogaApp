namespace YogaMarketplace.Api;

public record AdminProviderResponse(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string? Bio,
    int? Age,
    string? Email,
    string Phone,
    Guid AreaId,
    string Area,
    string City,
    string Status,
    bool OffersHome,
    bool OffersStudio,
    bool OffersOnline,
    decimal? HomeRate,
    decimal? StudioRate,
    decimal? OnlineRate,
    string? StudioAddress,
    string? GoogleMeetLink,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public class RejectProviderRequest
{
    public string? Reason { get; set; }
}

public record AdminCertificateResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string Title,
    string ContentType,
    long SizeBytes,
    string Status,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public class RejectCertificateRequest
{
    public string? Reason { get; set; }
}

public class UpdateContentPageRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public int? Version { get; set; }
}

public record AdminUserProvider(Guid Id, string DisplayName, string Status, string Area);

public record AdminUserSummary(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminUserProvider? Provider,
    bool IsBlocked);

public record AdminUserBlockEventResponse(
    Guid Id,
    string Action,
    string? Reason,
    Guid AdminUserId,
    string? AdminName,
    DateTimeOffset CreatedAt);

public record AdminUserDetail(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminProviderResponse? Provider,
    bool IsBlocked,
    DateTimeOffset? BlockedAt,
    string? BlockedReason,
    IReadOnlyList<AdminUserBlockEventResponse> BlockHistory);

public class AdminBlockUserRequest
{
    public string? Reason { get; set; }
}

public record AdminBookingResponse(
    Guid Id,
    Guid CustomerId,
    string? CustomerName,
    string CustomerPhone,
    Guid ProviderId,
    string ProviderName,
    Guid ServiceId,
    string ServiceTitle,
    Guid SlotId,
    string Mode,
    string Status,
    decimal Amount,
    decimal ConvenienceFee,
    decimal CommissionPercent,
    string Currency,
    DateOnly Date,
    string Start,
    string End,
    string? HomeAddress,
    string? Landmark,
    string? MeetLink,
    string? StudioAddress,
    string? PaymentStatus,
    Guid? PaymentId,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    int? ReviewRating,
    decimal? PayoutNet,
    string? PayoutStatus,
    DateTimeOffset CreatedAt,
    decimal? RefundedAmount,
    decimal? LateCancelFee,
    string? CancelledBy,
    string? CancelReason,
    bool PaidWithCredit,
    decimal? CreditValue);

public class AdminCancelBookingRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// A booking payment or a pack sale. <see cref="Kind"/> is "Booking" or "Pack". A pack row has no booking or provider
/// and carries <see cref="PackName"/> instead.
/// </summary>
public record AdminPaymentResponse(
    Guid Id,
    Guid? BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string? Gateway,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ProviderId,
    string? ProviderName,
    Guid CustomerId,
    string? CustomerName,
    string CustomerPhone,
    decimal RefundedAmount,
    string Kind = AdminPaymentKind.Booking,
    string? PackName = null);

public static class AdminPaymentKind
{
    public const string Booking = "Booking";
    public const string Pack = "Pack";
}

public record AdminPayoutResponse(
    Guid Id,
    Guid BookingId,
    Guid ProviderId,
    string ProviderName,
    decimal GrossAmount,
    decimal FeePercent,
    decimal FeeAmount,
    decimal NetAmount,
    string Status,
    DateTimeOffset CreatedAt,
    string BookingStatus);

public record AdminAreaResponse(Guid Id, string City, string Name, bool IsActive);

public record AdminCategoryResponse(Guid Id, string Name, string Slug, bool IsActive);

public class CreateAreaRequest
{
    public string? Name { get; set; }
    public string? City { get; set; }
}

public class PatchAreaRequest
{
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}

public class PatchCategoryRequest
{
    public string? Name { get; set; }
}

/// <summary>Omitted fields stay as they are. <see cref="Version"/> must match the row the admin edited.</summary>
public class UpdateSettingsRequest
{
    public int? Version { get; set; }
    public decimal? CommissionPercent { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public int? CancelFreeWindowHours { get; set; }
    public int? RescheduleFreeWindowHours { get; set; }
    public string? LateCancelFeeType { get; set; }
    public decimal? LateCancelFeeValue { get; set; }
    public string? PayoutCycle { get; set; }
    public string? PolicyNote { get; set; }
    public string? BannerTitle { get; set; }
    public string? BannerSubtitle { get; set; }
    public string? BannerOffer { get; set; }
    public bool? CreditConvenienceFee { get; set; }
    public string? CreditPayBasis { get; set; }
    public string? CreditLateCancel { get; set; }
    public bool? CreditBookingStandsAfterExpiry { get; set; }
    public bool? PackRefundsEnabled { get; set; }
    public decimal? PackReferenceSessionPrice { get; set; }
    public decimal? PackRefundFee { get; set; }

    /// <summary>Market-clock time as HH:mm.</summary>
    public string? ReconciliationTime { get; set; }
    public int? WebhookStuckMinutes { get; set; }
    public int? RefundPendingHours { get; set; }
    public int? PolicyVersion { get; set; }
    public int? WebhookPayloadRetentionDays { get; set; }
    public bool? ReconciliationEnabled { get; set; }
    public bool? CreditExpiryEnabled { get; set; }

    /// <summary>Market-clock time as HH:mm.</summary>
    public string? CreditExpiryTime { get; set; }
    public bool? WebhookCleanupEnabled { get; set; }

    /// <summary>Market-clock time as HH:mm.</summary>
    public string? WebhookCleanupTime { get; set; }
    public bool? EmailAlertsEnabled { get; set; }

    /// <summary>An empty text clears it.</summary>
    public string? ErrorAlertEmail { get; set; }
    public int? ErrorAlertThrottleMinutes { get; set; }
    public int? ErrorAlertCheckMinutes { get; set; }
    public int? ErrorEventRetentionDays { get; set; }
}

public record AdminSettingsResponse(
    int Version,
    string Currency,
    decimal CommissionPercent,
    decimal ConvenienceFee,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    string LateCancelFeeType,
    decimal LateCancelFeeValue,
    string PayoutCycle,
    DateTimeOffset PayoutPeriodStart,
    string PolicyNote,
    string? BannerTitle,
    string? BannerSubtitle,
    string? BannerOffer,
    DateTimeOffset? UpdatedAt,
    bool CreditConvenienceFee,
    string CreditPayBasis,
    string CreditLateCancel,
    bool CreditBookingStandsAfterExpiry,
    bool PackRefundsEnabled,
    decimal PackReferenceSessionPrice,
    decimal PackRefundFee,
    string ReconciliationTime,
    int WebhookStuckMinutes,
    int RefundPendingHours,
    int PolicyVersion,
    int WebhookPayloadRetentionDays,
    bool ReconciliationEnabled,
    bool CreditExpiryEnabled,
    string CreditExpiryTime,
    bool WebhookCleanupEnabled,
    string WebhookCleanupTime,
    bool EmailAlertsEnabled,
    string? ErrorAlertEmail,
    int ErrorAlertThrottleMinutes,
    int ErrorAlertCheckMinutes,
    int ErrorEventRetentionDays);

public record AdminErrorEventResponse(
    Guid Id,
    string ErrorType,
    string Level,
    string? Source,
    string LastMessage,
    string? LastCorrelationId,
    int Count,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? LastAlertedAt);

public record AdminErrorSummaryResponse(int Unacknowledged);

public record AdminJobResponse(
    Guid Id,
    string Type,
    string? Payload,
    string Status,
    DateTimeOffset RunAt,
    int Attempts,
    int MaxAttempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool Recurring);

public record ReconciliationRunResponse(
    Guid Id,
    DateOnly MarketDate,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    bool GatewayCompared,
    int ItemCount,
    int OpenCount,
    string? Error);

public record ReconciliationItemResponse(
    Guid Id,
    Guid RunId,
    DateOnly MarketDate,
    string Type,
    string Status,
    string? GatewayPaymentId,
    string? GatewayRefundId,
    string? Reference,
    string? DatabaseValue,
    string? GatewayValue,
    string Detail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByName,
    string? ResolutionNote);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public class ResolveReconciliationItemRequest
{
    public string? Note { get; set; }
}

public record AdminPackResponse(
    Guid Id,
    string Name,
    int Sessions,
    decimal Price,
    int ValidityDays,
    decimal PriceCap,
    bool IsActive,
    int Version,
    int Sold,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Create needs every field. Update needs <see cref="Version"/>; omitted fields stay as they are.</summary>
public class SavePackRequest
{
    public string? Name { get; set; }
    public int? Sessions { get; set; }
    public decimal? Price { get; set; }
    public int? ValidityDays { get; set; }
    public decimal? PriceCap { get; set; }
    public bool? IsActive { get; set; }
    public int? Version { get; set; }
}

/// <summary>
/// One purchase with the rules saved on it. <see cref="Status"/> is Active, UsedUp, Expired or Refunded.
/// <see cref="RefundPreview"/> is what an admin partial refund would pay now.
/// </summary>
public record AdminCreditBatchResponse(
    Guid Id,
    Guid PackId,
    string PackName,
    int Sessions,
    int Remaining,
    int Used,
    decimal Price,
    decimal PriceCap,
    string Currency,
    DateTimeOffset PurchasedAt,
    DateTimeOffset ExpiresAt,
    DateOnly UseBy,
    string Status,
    bool RefundsEnabled,
    decimal ReferenceSessionPrice,
    decimal RefundFee,
    decimal RefundPreview,
    decimal RefundedAmount,
    DateTimeOffset? RefundedAt,
    string GatewayPaymentId,
    string PayBasis,
    string LateCancel,
    bool BookingStandsAfterExpiry,
    bool ChargeConvenienceFee);

public record SettingsAuditResponse(
    Guid Id,
    DateTimeOffset ChangedAt,
    Guid AdminUserId,
    string? AdminName,
    string Field,
    string? OldValue,
    string? NewValue);

public record BookingStatusCount(string Status, int Count);

public record PendingPayoutTotals(int Count, decimal Gross, decimal Net);

/// <summary>
/// <see cref="GmvPaid"/> is money customers paid for bookings, net of refunds; ₹0 credit payment rows never count.
/// <see cref="PackSales"/> is pack payments net of pack refunds, and <see cref="Revenue"/> is the two together.
/// <see cref="PaidBookings"/> counts bookings paid with money; <see cref="CreditBookings"/> counts bookings made with a credit.
/// </summary>
public record AdminReportResponse(
    IReadOnlyList<BookingStatusCount> BookingsByStatus,
    decimal GmvPaid,
    string Currency,
    PendingPayoutTotals PendingPayouts,
    decimal PackSales,
    decimal Revenue,
    int PaidBookings,
    int CreditBookings);

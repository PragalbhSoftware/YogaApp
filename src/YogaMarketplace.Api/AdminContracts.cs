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

public record AdminUserProvider(Guid Id, string DisplayName, string Status, string Area);

public record AdminUserSummary(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminUserProvider? Provider);

public record AdminUserDetail(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminProviderResponse? Provider);

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
    DateTimeOffset CreatedAt);

public record AdminPaymentResponse(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string? Gateway,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ProviderId,
    string ProviderName,
    Guid CustomerId,
    string? CustomerName,
    string CustomerPhone);

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
    DateTimeOffset CreatedAt);

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

public class PatchPolicyRequest
{
    public decimal? PlatformFeePercent { get; set; }
    public int? CancelFreeWindowHours { get; set; }
    public int? RescheduleFreeWindowHours { get; set; }
    public decimal? LateCancelFeePercent { get; set; }
    public string? PolicyNote { get; set; }
}

public record BookingStatusCount(string Status, int Count);

public record PendingPayoutTotals(int Count, decimal Gross, decimal Net);

public record AdminReportResponse(
    IReadOnlyList<BookingStatusCount> BookingsByStatus,
    decimal GmvPaid,
    string Currency,
    PendingPayoutTotals PendingPayouts);

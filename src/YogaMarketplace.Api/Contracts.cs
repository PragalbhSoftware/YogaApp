using System.Text.Json.Serialization;

namespace YogaMarketplace.Api;

public record OtpResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DevCode);

public record UserResponse(Guid Id, string? Name, string Phone, string? Gender, string Role);

public record VerifyResponse(string Token, UserResponse User);

public class RequestOtpRequest
{
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public string? Gender { get; set; }
    public bool IsNewUser { get; set; }
}

public class VerifyOtpRequest
{
    public string? Phone { get; set; }
    public string? Code { get; set; }
}

public class ResendOtpRequest
{
    public string? Phone { get; set; }
}

public record AreaResponse(Guid Id, string City, string Name);

public record CategoryResponse(Guid Id, string Name, string Slug);

public record PolicyResponse(
    string Currency,
    decimal PlatformFeePercent,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    decimal LateCancelFeePercent,
    string PolicyNote);

public record ModeRate(string Mode, decimal Rate);

public record ProviderSummary(
    Guid Id,
    string DisplayName,
    string? Bio,
    string Area,
    string City,
    string Status,
    IReadOnlyList<ModeRate> Modes,
    decimal? RatingAverage,
    int ReviewCount);

public record ProviderDetail(
    Guid Id,
    string DisplayName,
    string? Bio,
    int? Age,
    string Area,
    string City,
    string Status,
    IReadOnlyList<ModeRate> Modes,
    string? StudioAddress,
    decimal? RatingAverage,
    int ReviewCount);

public record ProviderSelf(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string? Bio,
    int? Age,
    string? Email,
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
    string? RejectionReason);

public record RegisterProviderResponse(ProviderSelf Provider, string Token);

public class RegisterProviderRequest
{
    public string? DisplayName { get; set; }
    public int? Age { get; set; }
    public string? Email { get; set; }
    public Guid AreaId { get; set; }
    public string? Bio { get; set; }
    public bool OffersHome { get; set; }
    public bool OffersStudio { get; set; }
    public bool OffersOnline { get; set; }
    public decimal? HomeRate { get; set; }
    public decimal? StudioRate { get; set; }
    public decimal? OnlineRate { get; set; }
    public string? StudioAddress { get; set; }
    public string? GoogleMeetLink { get; set; }
    public Guid? CategoryId { get; set; }
}

public record SlotResponse(Guid Id, string Mode, DateOnly Date, string Start, string End);

public record SlotListResponse(string Mode, DateOnly From, DateOnly To, IReadOnlyList<SlotResponse> Slots);

public class AddSlotsRequest
{
    public string? Mode { get; set; }
    public List<SlotInput>? Slots { get; set; }
}

public class SlotInput
{
    public DateOnly Date { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}

public class CreateBookingOrderRequest
{
    public Guid SlotId { get; set; }
    public Guid? ServiceId { get; set; }
    public string? HomeAddress { get; set; }
    public string? Landmark { get; set; }
}

public record CheckoutOrderResponse(
    Guid CheckoutId,
    string KeyId,
    string OrderId,
    long AmountPaise,
    decimal Amount,
    string Currency,
    Guid SlotId,
    string Mode,
    string ProviderName);

public class ConfirmBookingPaymentRequest
{
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? Signature { get; set; }
}

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public record ReviewResponse(
    Guid Id,
    Guid BookingId,
    Guid ProviderId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

public record BookingResponse(
    Guid Id,
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
    string PaymentStatus,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    DateTimeOffset CreatedAt,
    bool HasReviewed);

using System.Text.Json.Serialization;

namespace YogaMarketplace.Api;

public record OtpResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DevCode);

public record UserResponse(Guid Id, string? Name, string Phone, string? Gender, string Role);

public record VisitAddressResponse(
    string Line1,
    string Area,
    string City,
    string Pin,
    string Landmark,
    string HomeAddress);

public record CustomerProfileResponse(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string Role,
    VisitAddressResponse? VisitAddress);

public class UpdateVisitAddressRequest
{
    public string? Line1 { get; set; }
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? Pin { get; set; }
    public string? Landmark { get; set; }
}

public class UpdateCustomerProfileRequest
{
    public string? Name { get; set; }
    public string? Gender { get; set; }
}

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

/// <summary>Terms for new bookings. Existing bookings keep the terms they were made with.</summary>
public record PolicyResponse(
    string Currency,
    decimal CommissionPercent,
    decimal ConvenienceFee,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    string LateCancelFeeType,
    decimal LateCancelFeeValue);

public record BannerResponse(string? Title, string? Subtitle, string? Offer);

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

public class UpdateProviderRatesRequest
{
    public decimal? HomeRate { get; set; }
    public decimal? StudioRate { get; set; }
    public decimal? OnlineRate { get; set; }
}

public class UpdateProviderProfileRequest
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
}

public record InstructorPayoutResponse(
    Guid Id,
    Guid BookingId,
    decimal GrossAmount,
    decimal FeePercent,
    decimal FeeAmount,
    decimal NetAmount,
    string Status,
    DateTimeOffset CreatedAt,
    string BookingStatus);

public record PublicReviewResponse(int Rating, string? Comment, string ReviewerName, DateTimeOffset CreatedAt);

public record SlotResponse(Guid Id, string Mode, DateOnly Date, string Start, string End);

public record SlotListResponse(string Mode, DateOnly From, DateOnly To, IReadOnlyList<SlotResponse> Slots);

public record OwnedSlotResponse(Guid Id, string Mode, DateOnly Date, string Start, string End, bool IsBlocked);

public record OwnedSlotListResponse(string Mode, DateOnly From, DateOnly To, IReadOnlyList<OwnedSlotResponse> Slots);

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

public class UpdateSlotRequest
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
    decimal SessionAmount,
    decimal ConvenienceFee,
    string Currency,
    Guid SlotId,
    string Mode,
    string ProviderName,
    bool LocalCapture);

    public class ConfirmBookingPaymentRequest
    {
        public string? OrderId { get; set; }
        public string? PaymentId { get; set; }
        public string? Signature { get; set; }
    }

    public class LocalConfirmRequest
    {
        public string? OrderId { get; set; }
    }

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class RescheduleBookingRequest
{
    public Guid SlotId { get; set; }
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
    decimal ConvenienceFee,
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
    bool HasReviewed,
    decimal RefundedAmount,
    decimal? LateCancelFee,
    string? CancelledBy,
    string? CancelReason);

/// <summary>
/// <see cref="Amount"/> is what the customer paid (session plus convenience fee). A late cancel keeps the
/// late fee and the convenience fee. Terms come from the booking, not from current settings.
/// </summary>
public record CancelQuoteResponse(
    decimal Amount,
    decimal SessionAmount,
    decimal ConvenienceFee,
    decimal LateCancelFee,
    decimal ConvenienceFeeKept,
    decimal Refund,
    string Currency,
    string LateCancelFeeType,
    decimal LateCancelFeeValue,
    DateTimeOffset? FreeUntil);

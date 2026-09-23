using System.Text.Json.Serialization;

namespace YogaMarketplace.Web.Services;

public static class BookingStatuses
{
    public const string PendingAccept = "PendingAccept";
    public const string Upcoming = "Upcoming";
    public const string Declined = "Declined";
    public const string Completed = "Completed";
    public const string NoShow = "NoShow";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        PendingAccept,
        Upcoming,
        Declined,
        Completed,
        NoShow,
        Cancelled
    ];

    public static bool TryNormalize(string? status, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(status))
            return true;

        canonical = All.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical is not null;
    }
}

public static class PaymentStatuses
{
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
}

public sealed class CreateBookingOrderDto
{
    public Guid SlotId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HomeAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Landmark { get; set; }
}

public sealed record CheckoutOrderDto(
    Guid CheckoutId,
    string KeyId,
    string OrderId,
    long AmountPaise,
    decimal Amount,
    string Currency,
    Guid SlotId,
    string Mode,
    string ProviderName);

public sealed class ConfirmPaymentDto
{
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? Signature { get; set; }
}

public sealed record BookingDto(
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
    DateTimeOffset CreatedAt);

public sealed class CreateReviewDto
{
    public int Rating { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }
}

public sealed record ReviewDto(
    Guid Id,
    Guid BookingId,
    Guid ProviderId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

/// <summary>
/// Customer booking calls: checkout, the customer's list, and one review after completion.
/// Instructor accept, decline, and complete are <see cref="IInstructorBookingApi"/>.
/// The Razorpay webhook stays on the API; this client does not call it.
/// </summary>
public interface IBookingApi
{
    Task<ApiResult<CheckoutOrderDto>> CreateOrderAsync(CreateBookingOrderDto request, CancellationToken cancellationToken);
    Task<ApiResult<BookingDto>> ConfirmAsync(ConfirmPaymentDto request, CancellationToken cancellationToken);
    Task<ApiResult<List<BookingDto>>> ListMineAsync(CancellationToken cancellationToken);
    Task<ApiResult<ReviewDto>> CreateReviewAsync(Guid bookingId, CreateReviewDto request, CancellationToken cancellationToken);
}

public sealed class BookingApiClient : IBookingApi
{
    private const string OrdersPath = "api/bookings/orders";
    private const string ConfirmPath = "api/bookings/confirm";
    private const string MinePath = "api/bookings/me";

    private readonly ApiExchange _exchange;

    public BookingApiClient(IHttpClientFactory factory, ILogger<BookingApiClient> logger)
    {
        _exchange = new ApiExchange(factory.CreateClient(MarketplaceApiClient.HttpClientName), logger);
    }

    public Task<ApiResult<CheckoutOrderDto>> CreateOrderAsync(CreateBookingOrderDto request, CancellationToken cancellationToken) =>
        _exchange.PostAsync<CheckoutOrderDto>(OrdersPath, request, cancellationToken);

    public Task<ApiResult<BookingDto>> ConfirmAsync(ConfirmPaymentDto request, CancellationToken cancellationToken) =>
        _exchange.PostAsync<BookingDto>(ConfirmPath, request, cancellationToken);

    public Task<ApiResult<List<BookingDto>>> ListMineAsync(CancellationToken cancellationToken) =>
        _exchange.GetAsync<List<BookingDto>>(MinePath, cancellationToken);

    public Task<ApiResult<ReviewDto>> CreateReviewAsync(Guid bookingId, CreateReviewDto request, CancellationToken cancellationToken) =>
        _exchange.PostAsync<ReviewDto>($"api/bookings/{bookingId}/reviews", request, cancellationToken);
}

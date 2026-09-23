namespace YogaMarketplace.Web.Services;

/// <summary>
/// Provider booking calls. Customers use <see cref="IBookingApi"/>.
/// </summary>
public interface IInstructorBookingApi
{
    Task<ApiResult<List<BookingDto>>> ListAsync(string? status, CancellationToken cancellationToken);
    Task<ApiResult<BookingDto>> AcceptAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<ApiResult<BookingDto>> DeclineAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<ApiResult<BookingDto>> CompleteAsync(Guid bookingId, CancellationToken cancellationToken);
}

public sealed class InstructorBookingApiClient : IInstructorBookingApi
{
    private const string InboxPath = "api/bookings/instructor";

    private readonly ApiExchange _exchange;

    public InstructorBookingApiClient(IHttpClientFactory factory, ILogger<InstructorBookingApiClient> logger)
    {
        _exchange = new ApiExchange(factory.CreateClient(MarketplaceApiClient.HttpClientName), logger);
    }

    public Task<ApiResult<List<BookingDto>>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrEmpty(status)
            ? InboxPath
            : $"{InboxPath}?status={Uri.EscapeDataString(status)}";
        return _exchange.GetAsync<List<BookingDto>>(path, cancellationToken);
    }

    public Task<ApiResult<BookingDto>> AcceptAsync(Guid bookingId, CancellationToken cancellationToken) =>
        PostAsync(bookingId, "accept", cancellationToken);

    public Task<ApiResult<BookingDto>> DeclineAsync(Guid bookingId, CancellationToken cancellationToken) =>
        PostAsync(bookingId, "decline", cancellationToken);

    public Task<ApiResult<BookingDto>> CompleteAsync(Guid bookingId, CancellationToken cancellationToken) =>
        PostAsync(bookingId, "complete", cancellationToken);

    private Task<ApiResult<BookingDto>> PostAsync(Guid bookingId, string action, CancellationToken cancellationToken) =>
        _exchange.PostAsync<BookingDto>($"api/bookings/{bookingId}/{action}", new { }, cancellationToken);
}

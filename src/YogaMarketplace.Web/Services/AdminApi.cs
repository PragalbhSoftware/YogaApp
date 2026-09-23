namespace YogaMarketplace.Web.Services;

/// <summary>Admin HTTP calls. Kept off <see cref="IMarketplaceApi"/> and <see cref="IBookingApi"/>.</summary>
public interface IAdminReportApi
{
    Task<ApiResult<AdminReportDto>> SummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Instructor approval queue. Verify and reject do not create bookings.</summary>
public interface IAdminProviderApi
{
    Task<ApiResult<List<AdminProviderDto>>> ListAsync(string status, CancellationToken cancellationToken);
    Task<ApiResult<AdminProviderDto>> VerifyAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiResult<AdminProviderDto>> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken);
}

public interface IAdminUserApi
{
    Task<ApiResult<List<AdminUserSummaryDto>>> ListAsync(string? query, string? role, CancellationToken cancellationToken);
    Task<ApiResult<AdminUserDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Read-only admin bookings. Customers use <see cref="IBookingApi"/>.</summary>
public interface IAdminBookingApi
{
    Task<ApiResult<List<AdminBookingDto>>> ListAsync(string? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<ApiResult<AdminBookingDto>> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Read-only payments and payouts.</summary>
public interface IAdminTransactionApi
{
    Task<ApiResult<List<AdminPaymentDto>>> PaymentsAsync(string? status, CancellationToken cancellationToken);
    Task<ApiResult<List<AdminPayoutDto>>> PayoutsAsync(string status, CancellationToken cancellationToken);
}

public interface IAdminMasterApi
{
    Task<ApiResult<List<AdminAreaDto>>> AreasAsync(CancellationToken cancellationToken);
    Task<ApiResult<AdminAreaDto>> CreateAreaAsync(string name, string? city, CancellationToken cancellationToken);
    Task<ApiResult<AdminAreaDto>> UpdateAreaAsync(Guid id, string name, bool isActive, CancellationToken cancellationToken);
    Task<ApiResult<List<AdminCategoryDto>>> CategoriesAsync(CancellationToken cancellationToken);
    Task<ApiResult<AdminCategoryDto>> RenameCategoryAsync(Guid id, string name, CancellationToken cancellationToken);
    Task<ApiResult<PolicyDto>> PolicyAsync(CancellationToken cancellationToken);
    Task<ApiResult<PolicyDto>> UpdatePolicyAsync(PolicyUpdate update, CancellationToken cancellationToken);
}

public sealed record AdminProviderDto(
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

public sealed record AdminUserProviderDto(Guid Id, string DisplayName, string Status, string Area);

public sealed record AdminUserSummaryDto(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminUserProviderDto? Provider);

public sealed record AdminUserDetailDto(
    Guid Id,
    string? Name,
    string Phone,
    string? Gender,
    string? Email,
    string Role,
    DateTimeOffset CreatedAt,
    AdminProviderDto? Provider);

public sealed record AdminBookingDto(
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

public sealed record AdminPaymentDto(
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

public sealed record AdminPayoutDto(
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

public sealed record AdminAreaDto(Guid Id, string City, string Name, bool IsActive);

public sealed record AdminCategoryDto(Guid Id, string Name, string Slug, bool IsActive);

public sealed record PolicyDto(
    string Currency,
    decimal PlatformFeePercent,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    decimal LateCancelFeePercent,
    string? PolicyNote);

public sealed record PolicyUpdate(
    decimal PlatformFeePercent,
    int CancelFreeWindowHours,
    int RescheduleFreeWindowHours,
    decimal LateCancelFeePercent,
    string PolicyNote);

public sealed record BookingStatusCountDto(string Status, int Count);

public sealed record PendingPayoutTotalsDto(int Count, decimal Gross, decimal Net);

public sealed record AdminReportDto(
    List<BookingStatusCountDto>? BookingsByStatus,
    decimal GmvPaid,
    string Currency,
    PendingPayoutTotalsDto? PendingPayouts);

internal abstract class AdminClientBase
{
    private readonly ApiExchange _exchange;

    protected AdminClientBase(IHttpClientFactory factory, ILogger logger)
    {
        _exchange = new ApiExchange(factory.CreateClient(MarketplaceApiClient.HttpClientName), logger);
    }

    protected ApiExchange Exchange => _exchange;
}

internal sealed class AdminReportClient : AdminClientBase, IAdminReportApi
{
    public AdminReportClient(IHttpClientFactory factory, ILogger<AdminReportClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<AdminReportDto>> SummaryAsync(CancellationToken cancellationToken) =>
        Exchange.GetAsync<AdminReportDto>("api/admin/reports/summary", cancellationToken);
}

internal sealed class AdminProviderClient : AdminClientBase, IAdminProviderApi
{
    public AdminProviderClient(IHttpClientFactory factory, ILogger<AdminProviderClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<List<AdminProviderDto>>> ListAsync(string status, CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminProviderDto>>(
            ApiQuery.With("api/admin/providers", ("status", status)),
            cancellationToken);

    public Task<ApiResult<AdminProviderDto>> VerifyAsync(Guid id, CancellationToken cancellationToken) =>
        Exchange.PostAsync<AdminProviderDto>($"api/admin/providers/{id}/verify", new { }, cancellationToken);

    public Task<ApiResult<AdminProviderDto>> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken) =>
        Exchange.PostAsync<AdminProviderDto>($"api/admin/providers/{id}/reject", new { reason }, cancellationToken);
}

internal sealed class AdminUserClient : AdminClientBase, IAdminUserApi
{
    public AdminUserClient(IHttpClientFactory factory, ILogger<AdminUserClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<List<AdminUserSummaryDto>>> ListAsync(string? query, string? role, CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminUserSummaryDto>>(
            ApiQuery.With("api/admin/users", ("q", query), ("role", role)),
            cancellationToken);

    public Task<ApiResult<AdminUserDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Exchange.GetAsync<AdminUserDetailDto>($"api/admin/users/{id}", cancellationToken);
}

internal sealed class AdminBookingClient : AdminClientBase, IAdminBookingApi
{
    public AdminBookingClient(IHttpClientFactory factory, ILogger<AdminBookingClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<List<AdminBookingDto>>> ListAsync(
        string? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminBookingDto>>(
            ApiQuery.With(
                "api/admin/bookings",
                ("status", status),
                ("from", AdminFormat.Date(from)),
                ("to", AdminFormat.Date(to))),
            cancellationToken);

    public Task<ApiResult<AdminBookingDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Exchange.GetAsync<AdminBookingDto>($"api/admin/bookings/{id}", cancellationToken);
}

internal sealed class AdminTransactionClient : AdminClientBase, IAdminTransactionApi
{
    public AdminTransactionClient(IHttpClientFactory factory, ILogger<AdminTransactionClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<List<AdminPaymentDto>>> PaymentsAsync(string? status, CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminPaymentDto>>(
            ApiQuery.With("api/admin/payments", ("status", status)),
            cancellationToken);

    public Task<ApiResult<List<AdminPayoutDto>>> PayoutsAsync(string status, CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminPayoutDto>>(
            ApiQuery.With("api/admin/payouts", ("status", status)),
            cancellationToken);
}

internal sealed class AdminMasterClient : AdminClientBase, IAdminMasterApi
{
    public AdminMasterClient(IHttpClientFactory factory, ILogger<AdminMasterClient> logger)
        : base(factory, logger)
    {
    }

    public Task<ApiResult<List<AdminAreaDto>>> AreasAsync(CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminAreaDto>>("api/admin/areas", cancellationToken);

    public Task<ApiResult<AdminAreaDto>> CreateAreaAsync(string name, string? city, CancellationToken cancellationToken) =>
        Exchange.PostAsync<AdminAreaDto>("api/admin/areas", new { name, city }, cancellationToken);

    public Task<ApiResult<AdminAreaDto>> UpdateAreaAsync(Guid id, string name, bool isActive, CancellationToken cancellationToken) =>
        Exchange.PatchAsync<AdminAreaDto>($"api/admin/areas/{id}", new { name, isActive }, cancellationToken);

    public Task<ApiResult<List<AdminCategoryDto>>> CategoriesAsync(CancellationToken cancellationToken) =>
        Exchange.GetAsync<List<AdminCategoryDto>>("api/admin/categories", cancellationToken);

    public Task<ApiResult<AdminCategoryDto>> RenameCategoryAsync(Guid id, string name, CancellationToken cancellationToken) =>
        Exchange.PatchAsync<AdminCategoryDto>($"api/admin/categories/{id}", new { name }, cancellationToken);

    public Task<ApiResult<PolicyDto>> PolicyAsync(CancellationToken cancellationToken) =>
        Exchange.GetAsync<PolicyDto>("api/admin/policy", cancellationToken);

    public Task<ApiResult<PolicyDto>> UpdatePolicyAsync(PolicyUpdate update, CancellationToken cancellationToken) =>
        Exchange.PatchAsync<PolicyDto>("api/admin/policy", new
        {
            update.PlatformFeePercent,
            update.CancelFreeWindowHours,
            update.RescheduleFreeWindowHours,
            update.LateCancelFeePercent,
            update.PolicyNote
        }, cancellationToken);
}

internal static class ApiQuery
{
    public static string With(string path, params (string Key, string? Value)[] pairs)
    {
        var parts = new List<string>(pairs.Length);
        foreach (var (key, value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            parts.Add(key + "=" + Uri.EscapeDataString(value.Trim()));
        }

        return parts.Count == 0 ? path : path + "?" + string.Join('&', parts);
    }
}

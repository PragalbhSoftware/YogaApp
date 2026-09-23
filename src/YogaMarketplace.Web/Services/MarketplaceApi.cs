using System.Net.Http.Headers;

namespace YogaMarketplace.Web.Services;

public class ApiOptions
{
    public const string Section = "Api";
    public string BaseUrl { get; set; } = "http://localhost:5080";
    public int TimeoutSeconds { get; set; } = 15;
    public string CategorySlug { get; set; } = "yoga";
    public bool ShowDevOtpHint { get; set; }
}

public sealed class ApiResult<T>
{
    public bool Ok { get; private init; }
    public T? Data { get; private init; }
    public string? Error { get; private init; }
    public bool Unreachable { get; private init; }

    public static ApiResult<T> Success(T data) => new() { Ok = true, Data = data };
    public static ApiResult<T> Fail(string error) => new() { Ok = false, Error = error };
    public static ApiResult<T> Down(string error) => new() { Ok = false, Error = error, Unreachable = true };
}

public record OtpRequestDto(string Phone, string? Name, string? Gender, bool IsNewUser);

public record OtpResponseDto(Guid ChallengeId, DateTimeOffset ExpiresAt, string? DevCode);

public record UserDto(Guid Id, string? Name, string Phone, string? Gender, string Role);

public record VerifyResponseDto(string Token, UserDto User);

public record AreaDto(Guid Id, string City, string Name);

public record ModeRateDto(string Mode, decimal Rate);

public record ProviderSummaryDto(
    Guid Id,
    string DisplayName,
    string? Bio,
    string Area,
    string City,
    string Status,
    List<ModeRateDto> Modes,
    decimal? RatingAverage,
    int ReviewCount);

public record ProviderDetailDto(
    Guid Id,
    string DisplayName,
    string? Bio,
    int? Age,
    string Area,
    string City,
    string Status,
    List<ModeRateDto> Modes,
    string? StudioAddress,
    decimal? RatingAverage,
    int ReviewCount);

public record SlotDto(Guid Id, string Mode, DateOnly Date, string Start, string End);

public record SlotListDto(string Mode, DateOnly From, DateOnly To, List<SlotDto> Slots);

public interface IMarketplaceApi
{
    Task<ApiResult<OtpResponseDto>> RequestOtpAsync(OtpRequestDto request, CancellationToken cancellationToken);
    Task<ApiResult<OtpResponseDto>> ResendOtpAsync(string phone, CancellationToken cancellationToken);
    Task<ApiResult<VerifyResponseDto>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken);
    Task<ApiResult<List<AreaDto>>> GetAreasAsync(CancellationToken cancellationToken);
    Task<ApiResult<List<ProviderSummaryDto>>> BrowseAsync(string? area, string? mode, string? category, CancellationToken cancellationToken);
    Task<ApiResult<ProviderDetailDto>> GetProviderAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiResult<SlotListDto>> GetSlotsAsync(Guid id, string mode, CancellationToken cancellationToken);
}

public sealed class MarketplaceApiClient : IMarketplaceApi
{
    public const string HttpClientName = "marketplace";

    private readonly ApiExchange _exchange;

    public MarketplaceApiClient(IHttpClientFactory factory, ILogger<MarketplaceApiClient> logger)
    {
        _exchange = new ApiExchange(factory.CreateClient(HttpClientName), logger);
    }

    public Task<ApiResult<OtpResponseDto>> RequestOtpAsync(OtpRequestDto request, CancellationToken cancellationToken) =>
        _exchange.PostAsync<OtpResponseDto>("api/auth/otp/request", new
        {
            phone = request.Phone,
            name = request.IsNewUser ? request.Name : null,
            gender = request.IsNewUser ? request.Gender : null,
            isNewUser = request.IsNewUser
        }, cancellationToken);

    public Task<ApiResult<OtpResponseDto>> ResendOtpAsync(string phone, CancellationToken cancellationToken) =>
        _exchange.PostAsync<OtpResponseDto>("api/auth/otp/resend", new { phone }, cancellationToken);

    public Task<ApiResult<VerifyResponseDto>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken) =>
        _exchange.PostAsync<VerifyResponseDto>("api/auth/otp/verify", new { phone, code }, cancellationToken);

    public Task<ApiResult<List<AreaDto>>> GetAreasAsync(CancellationToken cancellationToken) =>
        _exchange.GetAsync<List<AreaDto>>("api/areas", cancellationToken);

    public Task<ApiResult<List<ProviderSummaryDto>>> BrowseAsync(
        string? area,
        string? mode,
        string? category,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(area))
            query.Add("area=" + Uri.EscapeDataString(area.Trim()));
        if (!string.IsNullOrWhiteSpace(mode))
            query.Add("mode=" + Uri.EscapeDataString(mode.Trim()));
        if (!string.IsNullOrWhiteSpace(category))
            query.Add("category=" + Uri.EscapeDataString(category.Trim()));

        var path = query.Count == 0 ? "api/providers" : "api/providers?" + string.Join('&', query);
        return _exchange.GetAsync<List<ProviderSummaryDto>>(path, cancellationToken);
    }

    public Task<ApiResult<ProviderDetailDto>> GetProviderAsync(Guid id, CancellationToken cancellationToken) =>
        _exchange.GetAsync<ProviderDetailDto>($"api/providers/{id}", cancellationToken);

    public Task<ApiResult<SlotListDto>> GetSlotsAsync(Guid id, string mode, CancellationToken cancellationToken) =>
        _exchange.GetAsync<SlotListDto>($"api/providers/{id}/slots?mode={Uri.EscapeDataString(mode)}", cancellationToken);
}

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public BearerTokenHandler(IHttpContextAccessor http)
    {
        _http = http;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _http.HttpContext?.User.FindFirst(AuthSession.ApiTokenClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}

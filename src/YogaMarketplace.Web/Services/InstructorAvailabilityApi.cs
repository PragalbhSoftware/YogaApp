using System.Globalization;

namespace YogaMarketplace.Web.Services;

public sealed record OwnedSlotDto(Guid Id, string Mode, DateOnly Date, string Start, string End, bool IsBlocked);

public sealed record OwnedSlotListDto(string Mode, DateOnly From, DateOnly To, List<OwnedSlotDto>? Slots);

/// <summary>
/// The signed-in instructor's slots. Public browse uses <see cref="IMarketplaceApi.GetSlotsAsync"/>.
/// </summary>
public interface IInstructorAvailabilityApi
{
    Task<ApiResult<OwnedSlotListDto>> ListAsync(string mode, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<ApiResult<OwnedSlotDto>> BlockAsync(Guid slotId, CancellationToken cancellationToken);
}

public sealed class InstructorAvailabilityApiClient : IInstructorAvailabilityApi
{
    private readonly ApiExchange _exchange;

    public InstructorAvailabilityApiClient(IHttpClientFactory factory, ILogger<InstructorAvailabilityApiClient> logger)
    {
        _exchange = new ApiExchange(factory.CreateClient(MarketplaceApiClient.HttpClientName), logger);
    }

    public Task<ApiResult<OwnedSlotListDto>> ListAsync(
        string mode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = new List<string> { "mode=" + Uri.EscapeDataString(mode) };
        if (from is DateOnly start)
            query.Add("from=" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (to is DateOnly end)
            query.Add("to=" + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return _exchange.GetAsync<OwnedSlotListDto>("api/providers/me/slots?" + string.Join('&', query), cancellationToken);
    }

    public Task<ApiResult<OwnedSlotDto>> BlockAsync(Guid slotId, CancellationToken cancellationToken) =>
        _exchange.PostAsync<OwnedSlotDto>($"api/providers/me/slots/{slotId}/block", new { }, cancellationToken);
}

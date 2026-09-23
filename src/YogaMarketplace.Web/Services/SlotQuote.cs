using YogaMarketplace.Web.Copy;

namespace YogaMarketplace.Web.Services;

public sealed record SlotQuote(
    Guid ProviderId,
    string ProviderName,
    string Area,
    string City,
    Guid SlotId,
    string Mode,
    DateOnly Date,
    string Start,
    string End,
    decimal Rate,
    string? StudioAddress);

public interface ISlotQuoteReader
{
    Task<ApiResult<SlotQuote>> ReadAsync(Guid providerId, Guid slotId, string? mode, CancellationToken cancellationToken);
}

public sealed class SlotQuoteReader : ISlotQuoteReader
{
    private readonly IMarketplaceApi _catalog;

    public SlotQuoteReader(IMarketplaceApi catalog)
    {
        _catalog = catalog;
    }

    public async Task<ApiResult<SlotQuote>> ReadAsync(
        Guid providerId,
        Guid slotId,
        string? mode,
        CancellationToken cancellationToken)
    {
        if (providerId == Guid.Empty || slotId == Guid.Empty)
            return ApiResult<SlotQuote>.Fail(UiCopy.SlotUnavailable);

        var normalized = SessionModes.Normalize(mode);
        if (normalized is null)
            return ApiResult<SlotQuote>.Fail(UiCopy.ModeRequired);

        var provider = await _catalog.GetProviderAsync(providerId, cancellationToken);
        if (!provider.Ok || provider.Data is null)
        {
            return provider.Unreachable
                ? ApiResult<SlotQuote>.Down(provider.Error ?? UiCopy.ApiUnreachable)
                : ApiResult<SlotQuote>.Fail(provider.Error ?? UiCopy.InstructorNotFound);
        }

        if (!string.Equals(provider.Data.Status, ProviderStatuses.Verified, StringComparison.OrdinalIgnoreCase))
            return ApiResult<SlotQuote>.Fail(UiCopy.InstructorNotFound);

        var slots = await _catalog.GetSlotsAsync(providerId, normalized, cancellationToken);
        if (!slots.Ok || slots.Data is null)
        {
            return slots.Unreachable
                ? ApiResult<SlotQuote>.Down(slots.Error ?? UiCopy.ApiUnreachable)
                : ApiResult<SlotQuote>.Fail(slots.Error ?? UiCopy.SlotUnavailable);
        }

        var slot = (slots.Data.Slots ?? []).FirstOrDefault(item => item.Id == slotId);
        if (slot is null)
            return ApiResult<SlotQuote>.Fail(UiCopy.SlotUnavailable);
        if (SessionClock.HasEnded(slot.Date, slot.End))
            return ApiResult<SlotQuote>.Fail(UiCopy.SlotEndedError);

        var rate = (provider.Data.Modes ?? [])
            .FirstOrDefault(item => string.Equals(item.Mode, normalized, StringComparison.OrdinalIgnoreCase))
            ?.Rate ?? 0m;

        return ApiResult<SlotQuote>.Success(new SlotQuote(
            provider.Data.Id,
            provider.Data.DisplayName,
            provider.Data.Area,
            provider.Data.City,
            slot.Id,
            normalized,
            slot.Date,
            slot.Start,
            slot.End,
            rate,
            provider.Data.StudioAddress));
    }
}

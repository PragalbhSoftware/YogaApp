using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Instructors;

public class ProfileModel : PageModel
{
    private readonly IMarketplaceApi _api;

    public ProfileModel(IMarketplaceApi api)
    {
        _api = api;
    }

    public ProviderDetailDto? Instructor { get; private set; }
    public string Mode { get; private set; } = "Home";
    public List<SlotDto> Slots { get; private set; } = [];
    public DateOnly? From { get; private set; }
    public DateOnly? To { get; private set; }
    public string? Error { get; private set; }
    public string? SlotsError { get; private set; }
    public bool Unreachable { get; private set; }

    public async Task OnGetAsync(Guid id, string? mode, CancellationToken cancellationToken)
    {
        var provider = await _api.GetProviderAsync(id, cancellationToken);
        if (!provider.Ok || provider.Data is null)
        {
            Error = provider.Error ?? UiCopy.InstructorNotFound;
            Unreachable = provider.Unreachable;
            return;
        }

        if (!string.Equals(provider.Data.Status, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            Error = UiCopy.InstructorNotFound;
            return;
        }

        Instructor = provider.Data with { Modes = provider.Data.Modes ?? [] };
        Mode = SessionModes.Normalize(mode)
            ?? Instructor.Modes.Select(m => SessionModes.Normalize(m.Mode)).FirstOrDefault(m => m is not null)
            ?? "Home";

        var slots = await _api.GetSlotsAsync(id, Mode, cancellationToken);
        if (!slots.Ok || slots.Data is null)
        {
            SlotsError = slots.Error ?? UiCopy.GenericError;
            return;
        }

        From = slots.Data.From;
        To = slots.Data.To;
        Slots = (slots.Data.Slots ?? [])
            .OrderBy(slot => slot.Date)
            .ThenBy(slot => slot.Start, StringComparer.Ordinal)
            .ToList();
    }
}

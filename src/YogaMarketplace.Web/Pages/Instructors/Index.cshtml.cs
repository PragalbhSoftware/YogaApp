using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Instructors;

public class IndexModel : PageModel
{
    private readonly IMarketplaceApi _api;
    private readonly ApiOptions _options;

    public IndexModel(IMarketplaceApi api, IOptions<ApiOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public string Area { get; private set; } = "";
    public string? Mode { get; private set; }
    public string ModePhrase => Mode ?? UiCopy.AnyModePhrase;
    public string? ModeLabel => Mode switch
    {
        SessionModes.Home => UiCopy.ModeHome,
        SessionModes.Studio => UiCopy.ModeStudio,
        SessionModes.Online => UiCopy.ModeOnline,
        _ => null
    };

    public string DocumentTitle => ModeLabel is null
        ? string.Format(UiCopy.BrowseDocumentTitle, Area)
        : string.Format(UiCopy.BrowseDocumentTitleWithMode, ModeLabel, Area);

    public string MetaDescription => ModeLabel is null
        ? string.Format(UiCopy.BrowseDescription, Area)
        : string.Format(UiCopy.BrowseDescriptionWithMode, ModeLabel, Area);

    public string ResultsLabel => Instructors.Count == 1
        ? UiCopy.OneResult
        : string.Format(UiCopy.ResultCount, Instructors.Count);

    public List<string> AreaChoices { get; private set; } = [];
    public List<ModeOption> ModeOptions { get; private set; } = [];
    public List<ProviderSummaryDto> Instructors { get; private set; } = [];
    public string? Error { get; private set; }
    public string? AreasError { get; private set; }
    public bool Unreachable { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? area, string? mode, CancellationToken cancellationToken)
    {
        if (await LoadAsync(area, mode, includeAreas: true, cancellationToken) is { } redirect)
            return redirect;
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync(string? area, string? mode, CancellationToken cancellationToken)
    {
        if (await LoadAsync(area, mode, includeAreas: false, cancellationToken) is { } redirect)
            return redirect;

        Response.Headers["X-Robots-Tag"] = PageSeo.RobotsNoIndex;
        return Partial("_InstructorList", this);
    }

    private async Task<IActionResult?> LoadAsync(
        string? area,
        string? mode,
        bool includeAreas,
        CancellationToken cancellationToken)
    {
        var saved = AreaCookie.Read(Request);
        Area = string.IsNullOrWhiteSpace(area) ? saved ?? "" : area.Trim();
        if (string.IsNullOrWhiteSpace(Area))
            return RedirectToPage("/Areas/Select");

        if (!string.Equals(Area, saved, StringComparison.Ordinal))
            AreaCookie.Write(HttpContext, Area);

        Mode = SessionModes.Normalize(mode);
        ModeOptions =
        [
            new ModeOption("", UiCopy.AnyMode, Mode is null),
            new ModeOption(SessionModes.Home, UiCopy.ModeHome, Mode == SessionModes.Home),
            new ModeOption(SessionModes.Studio, UiCopy.ModeStudio, Mode == SessionModes.Studio),
            new ModeOption(SessionModes.Online, UiCopy.ModeOnline, Mode == SessionModes.Online)
        ];

        if (includeAreas)
            await LoadAreasAsync(cancellationToken);

        var providers = await _api.BrowseAsync(Area, Mode, _options.CategorySlug, cancellationToken);
        if (!providers.Ok || providers.Data is null)
        {
            Error = providers.Error ?? UiCopy.GenericError;
            Unreachable = providers.Unreachable;
            Instructors = [];
            return null;
        }

        Instructors = providers.Data
            .Where(p => string.Equals(p.Status, ProviderStatuses.Verified, StringComparison.OrdinalIgnoreCase))
            .Select(p => p with { Modes = p.Modes ?? [] })
            .ToList();
        return null;
    }

    private async Task LoadAreasAsync(CancellationToken cancellationToken)
    {
        var areas = await _api.GetAreasAsync(cancellationToken);
        if (!areas.Ok || areas.Data is null)
        {
            AreasError = areas.Error ?? UiCopy.GenericError;
            AreaChoices = [Area];
            return;
        }

        AreaChoices = areas.Data
            .Select(a => a.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!AreaChoices.Contains(Area, StringComparer.OrdinalIgnoreCase))
            AreaChoices.Insert(0, Area);
    }

    public sealed record ModeOption(string Value, string Label, bool Selected);
}

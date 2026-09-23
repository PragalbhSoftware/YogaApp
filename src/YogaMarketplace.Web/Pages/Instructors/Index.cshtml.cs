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
    public List<string> AreaChoices { get; private set; } = [];
    public List<ModeOption> ModeOptions { get; private set; } = [];
    public List<ProviderSummaryDto> Instructors { get; private set; } = [];
    public string? Error { get; private set; }
    public string? AreasError { get; private set; }
    public bool Unreachable { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? area, string? mode, CancellationToken cancellationToken)
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
            new ModeOption(SessionModes.All[0], UiCopy.ModeHome, Mode == "Home"),
            new ModeOption(SessionModes.All[1], UiCopy.ModeStudio, Mode == "Studio"),
            new ModeOption(SessionModes.All[2], UiCopy.ModeOnline, Mode == "Online")
        ];

        var areas = await _api.GetAreasAsync(cancellationToken);
        if (!areas.Ok || areas.Data is null)
        {
            AreasError = areas.Error ?? UiCopy.GenericError;
            AreaChoices = [Area];
        }
        else
        {
            AreaChoices = areas.Data
                .Select(a => a.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!AreaChoices.Contains(Area, StringComparer.OrdinalIgnoreCase))
                AreaChoices.Insert(0, Area);
        }

        var providers = await _api.BrowseAsync(Area, Mode, _options.CategorySlug, cancellationToken);
        if (!providers.Ok || providers.Data is null)
        {
            Error = providers.Error ?? UiCopy.GenericError;
            Unreachable = providers.Unreachable;
            Instructors = [];
            return Page();
        }

        Instructors = providers.Data
            .Where(p => string.Equals(p.Status, "Verified", StringComparison.OrdinalIgnoreCase))
            .Select(p => p with { Modes = p.Modes ?? [] })
            .ToList();
        return Page();
    }

    public sealed record ModeOption(string Value, string Label, bool Selected);
}

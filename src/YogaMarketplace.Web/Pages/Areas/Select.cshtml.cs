using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Areas;

public class SelectModel : PageModel
{
    private readonly IMarketplaceApi _api;

    public SelectModel(IMarketplaceApi api)
    {
        _api = api;
    }

    public List<AreaDto> Areas { get; private set; } = [];
    public string? Selected { get; private set; }
    public string? Error { get; private set; }
    public bool Unreachable { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(string? area, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            Error = UiCopy.ChooseArea;
            await LoadAsync(cancellationToken);
            return Page();
        }

        AreaCookie.Write(HttpContext, area.Trim());
        return RedirectToPage("/Instructors/Index");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Selected = AreaCookie.Read(Request);
        var result = await _api.GetAreasAsync(cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            Unreachable = result.Unreachable;
            Areas = [];
            return;
        }

        Areas = result.Data
            .OrderBy(a => a.City, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

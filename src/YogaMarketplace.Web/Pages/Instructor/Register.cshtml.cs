using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Instructor;

public class RegisterModel : PageModel
{
    private readonly IMarketplaceApi _api;

    public RegisterModel(IMarketplaceApi api)
    {
        _api = api;
    }

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public int? Age { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public Guid AreaId { get; set; }

    [BindProperty]
    public string? Bio { get; set; }

    [BindProperty]
    public bool OffersHome { get; set; }

    [BindProperty]
    public bool OffersStudio { get; set; }

    [BindProperty]
    public bool OffersOnline { get; set; }

    [BindProperty]
    public decimal? HomeRate { get; set; }

    [BindProperty]
    public decimal? StudioRate { get; set; }

    [BindProperty]
    public decimal? OnlineRate { get; set; }

    [BindProperty]
    public string? StudioAddress { get; set; }

    [BindProperty]
    public string? GoogleMeetLink { get; set; }

    public ProviderSelfDto? Profile { get; private set; }
    public List<AreaDto> Areas { get; private set; } = [];
    public bool ShowForm { get; private set; }
    public string? Error { get; private set; }

    public string? StatusLead => Profile?.Status switch
    {
        ProviderApprovalStatuses.Pending => UiCopy.RegisterPendingLead,
        ProviderApprovalStatuses.Verified => UiCopy.RegisterVerifiedLead,
        ProviderApprovalStatuses.Rejected => UiCopy.RegisterRejectedLead,
        _ => Profile?.Status
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var mine = await _api.GetMineAsync(cancellationToken);
        if (mine.Ok && mine.Data is not null)
        {
            Profile = mine.Data;
            return;
        }

        if (mine.StatusCode == (int)HttpStatusCode.NotFound)
        {
            ShowForm = true;
            await LoadAreasAsync(cancellationToken);
            return;
        }

        Error = mine.Error ?? UiCopy.GenericError;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Error = FirstModelError() ?? UiCopy.GenericError;
            ShowForm = true;
            await LoadAreasAsync(cancellationToken);
            return Page();
        }

        var result = await _api.RegisterProviderAsync(ToRequest(), cancellationToken);
        if (result.Ok && result.Data is not null)
        {
            await AuthSession.SignInProviderAsync(HttpContext, result.Data.Token, result.Data.Provider);
            return RedirectToPage();
        }

        if (result.StatusCode == (int)HttpStatusCode.Conflict)
        {
            var mine = await _api.GetMineAsync(cancellationToken);
            if (mine.Ok && mine.Data is not null)
            {
                Profile = mine.Data;
                return Page();
            }
        }

        Error = result.Error ?? UiCopy.GenericError;
        ShowForm = true;
        await LoadAreasAsync(cancellationToken);
        return Page();
    }

    private RegisterProviderRequestDto ToRequest() => new(
        DisplayName?.Trim(),
        Age,
        BlankToNull(Email),
        AreaId,
        BlankToNull(Bio),
        OffersHome,
        OffersStudio,
        OffersOnline,
        HomeRate,
        StudioRate,
        OnlineRate,
        BlankToNull(StudioAddress),
        BlankToNull(GoogleMeetLink));

    private async Task LoadAreasAsync(CancellationToken cancellationToken)
    {
        var areas = await _api.GetAreasAsync(cancellationToken);
        if (!areas.Ok || areas.Data is null)
        {
            Error ??= areas.Error ?? UiCopy.GenericError;
            Areas = [];
            return;
        }

        Areas = areas.Data
            .OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? FirstModelError() =>
        ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

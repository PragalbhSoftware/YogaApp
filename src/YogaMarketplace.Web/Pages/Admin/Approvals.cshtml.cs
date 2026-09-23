using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin;

public class ApprovalsModel : PageModel
{
    private readonly IAdminProviderApi _providers;

    public ApprovalsModel(IAdminProviderApi providers)
    {
        _providers = providers;
    }

    public List<AdminProviderDto> Providers { get; private set; } = [];
    public string Status { get; private set; } = ProviderApprovalStatuses.Pending;
    public bool StatusKnown { get; private set; } = true;
    public string? Error { get; private set; }
    public string? Notice { get; private set; }

    public async Task OnGetAsync(string? status, string? notice, CancellationToken cancellationToken)
    {
        Notice = AdminNotices.Text(notice);
        await LoadAsync(status, cancellationToken);
    }

    public Task<IActionResult> OnPostVerifyAsync(Guid id, string? status, CancellationToken cancellationToken) =>
        ActAsync(id, status, () => _providers.VerifyAsync(id, cancellationToken), AdminNotices.Verified, cancellationToken);

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? status, string? reason, CancellationToken cancellationToken)
    {
        if (!ProviderApprovalStatuses.TryNormalize(status, out var canonical))
        {
            Error = UiCopy.UnknownProviderStatus;
            await LoadAsync(ProviderApprovalStatuses.Pending, cancellationToken);
            return Page();
        }

        if (!AdminInput.TryReason(reason, out var normalized, out var reasonError))
        {
            Error = reasonError;
            await LoadAsync(canonical, cancellationToken);
            return Page();
        }

        var result = await _providers.RejectAsync(id, normalized, cancellationToken);
        if (!result.Ok)
        {
            Error = result.Error ?? UiCopy.GenericError;
            await LoadAsync(canonical, cancellationToken);
            return Page();
        }

        return RedirectToPage(new { status = canonical, notice = AdminNotices.Rejected });
    }

    private async Task<IActionResult> ActAsync(
        Guid id,
        string? status,
        Func<Task<ApiResult<AdminProviderDto>>> action,
        string notice,
        CancellationToken cancellationToken)
    {
        if (!ProviderApprovalStatuses.TryNormalize(status, out var canonical))
        {
            Error = UiCopy.UnknownProviderStatus;
            await LoadAsync(ProviderApprovalStatuses.Pending, cancellationToken);
            return Page();
        }

        var result = await action();
        if (!result.Ok)
        {
            Error = result.Error ?? UiCopy.GenericError;
            await LoadAsync(canonical, cancellationToken);
            return Page();
        }

        return RedirectToPage(new { status = canonical, notice });
    }

    private async Task LoadAsync(string? status, CancellationToken cancellationToken)
    {
        if (!ProviderApprovalStatuses.TryNormalize(status, out var canonical))
        {
            StatusKnown = false;
            Status = ProviderApprovalStatuses.Pending;
            Error ??= UiCopy.UnknownProviderStatus;
            Providers = [];
            return;
        }

        StatusKnown = true;
        Status = canonical;
        var list = await _providers.ListAsync(canonical, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error ??= list.Error ?? UiCopy.GenericError;
            Providers = [];
            return;
        }

        Providers = list.Data;
    }
}

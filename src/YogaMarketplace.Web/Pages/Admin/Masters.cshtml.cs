using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin;

public class MastersModel : PageModel
{
    private readonly IAdminMasterApi _masters;

    public MastersModel(IAdminMasterApi masters)
    {
        _masters = masters;
    }

    public List<AdminAreaDto> Areas { get; private set; } = [];
    public List<AdminCategoryDto> Categories { get; private set; } = [];
    public PolicyDto? Policy { get; private set; }
    public bool AreasLoaded { get; private set; }
    public bool CategoriesLoaded { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string? DraftAreaName { get; private set; }
    public string? DraftCity { get; private set; }
    public string? PolicyFee { get; private set; }
    public string? CancelHours { get; private set; }
    public string? RescheduleHours { get; private set; }
    public string? LateFee { get; private set; }
    public string? PolicyNote { get; private set; }

    private bool _keepPolicyDraft;

    public async Task OnGetAsync(string? notice, CancellationToken cancellationToken)
    {
        Notice = AdminNotices.Text(notice);
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAreaAsync(string? name, string? city, CancellationToken cancellationToken)
    {
        DraftAreaName = name;
        DraftCity = city;
        if (!AdminInput.TryLabel(name, out var areaName, out var error))
        {
            Error = error;
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (!AdminInput.TryCity(city, out var areaCity, out error))
        {
            Error = error;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var created = await _masters.CreateAreaAsync(areaName, areaCity, cancellationToken);
        if (!created.Ok)
        {
            Error = created.Error ?? UiCopy.GenericError;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { notice = AdminNotices.AreaSaved });
    }

    public async Task<IActionResult> OnPostUpdateAreaAsync(Guid id, string? name, bool isActive, CancellationToken cancellationToken)
    {
        if (!AdminInput.TryLabel(name, out var areaName, out var error))
        {
            Error = error;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var updated = await _masters.UpdateAreaAsync(id, areaName, isActive, cancellationToken);
        if (!updated.Ok)
        {
            Error = updated.Error ?? UiCopy.GenericError;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { notice = AdminNotices.AreaSaved });
    }

    public async Task<IActionResult> OnPostRenameCategoryAsync(Guid id, string? name, CancellationToken cancellationToken)
    {
        if (!AdminInput.TryLabel(name, out var label, out var error))
        {
            Error = error;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var updated = await _masters.RenameCategoryAsync(id, label, cancellationToken);
        if (!updated.Ok)
        {
            Error = updated.Error ?? UiCopy.GenericError;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { notice = AdminNotices.CategorySaved });
    }

    public async Task<IActionResult> OnPostUpdatePolicyAsync(
        string? platformFeePercent,
        string? cancelFreeWindowHours,
        string? rescheduleFreeWindowHours,
        string? lateCancelFeePercent,
        string? policyNote,
        CancellationToken cancellationToken)
    {
        UsePolicyDraft(platformFeePercent, cancelFreeWindowHours, rescheduleFreeWindowHours, lateCancelFeePercent, policyNote);
        if (!TryPolicy(platformFeePercent, cancelFreeWindowHours, rescheduleFreeWindowHours, lateCancelFeePercent, policyNote, out var update, out var error))
        {
            Error = error;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var updated = await _masters.UpdatePolicyAsync(update, cancellationToken);
        if (!updated.Ok)
        {
            Error = updated.Error ?? UiCopy.GenericError;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { notice = AdminNotices.PolicySaved });
    }

    private void UsePolicyDraft(string? fee, string? cancelHours, string? rescheduleHours, string? lateFee, string? note)
    {
        _keepPolicyDraft = true;
        PolicyFee = fee;
        CancelHours = cancelHours;
        RescheduleHours = rescheduleHours;
        LateFee = lateFee;
        PolicyNote = note;
    }

    private static bool TryPolicy(
        string? feeText,
        string? cancelText,
        string? rescheduleText,
        string? lateText,
        string? noteText,
        out PolicyUpdate update,
        out string? error)
    {
        update = new PolicyUpdate(0, 0, 0, 0, "");
        if (!AdminInput.TryPercent(feeText, UiCopy.PlatformFee, out var fee, out error)
            || !AdminInput.TryHours(cancelText, UiCopy.CancelWindow, out var cancelHours, out error)
            || !AdminInput.TryHours(rescheduleText, UiCopy.RescheduleWindow, out var rescheduleHours, out error)
            || !AdminInput.TryPercent(lateText, UiCopy.LateCancelFee, out var lateFee, out error)
            || !AdminInput.TryNote(noteText, out var note, out error))
        {
            return false;
        }

        update = new PolicyUpdate(fee, cancelHours, rescheduleHours, lateFee, note);
        return true;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var areas = await _masters.AreasAsync(cancellationToken);
        if (areas.Ok && areas.Data is not null)
        {
            Areas = areas.Data;
            AreasLoaded = true;
        }
        else
            Error ??= areas.Error ?? UiCopy.GenericError;

        var categories = await _masters.CategoriesAsync(cancellationToken);
        if (categories.Ok && categories.Data is not null)
        {
            Categories = categories.Data;
            CategoriesLoaded = true;
        }
        else
            Error ??= categories.Error ?? UiCopy.GenericError;

        var policy = await _masters.PolicyAsync(cancellationToken);
        if (!policy.Ok || policy.Data is null)
        {
            Error ??= policy.Error ?? UiCopy.GenericError;
            return;
        }

        Policy = policy.Data;
        if (_keepPolicyDraft)
            return;

        PolicyFee = AdminFormat.Decimal(policy.Data.PlatformFeePercent);
        CancelHours = policy.Data.CancelFreeWindowHours.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RescheduleHours = policy.Data.RescheduleFreeWindowHours.ToString(System.Globalization.CultureInfo.InvariantCulture);
        LateFee = AdminFormat.Decimal(policy.Data.LateCancelFeePercent);
        PolicyNote = policy.Data.PolicyNote;
    }
}

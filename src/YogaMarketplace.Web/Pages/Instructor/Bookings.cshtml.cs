using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Instructor;

public class BookingsModel : PageModel
{
    private readonly IInstructorBookingApi _bookings;

    public BookingsModel(IInstructorBookingApi bookings)
    {
        _bookings = bookings;
    }

    public List<BookingDto> Bookings { get; private set; } = [];
    public string? Status { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }

    public async Task OnGetAsync(string? status, string? notice, CancellationToken cancellationToken)
    {
        Notice = InstructorNotices.Text(notice);
        await LoadAsync(status, cancellationToken);
    }

    public Task<IActionResult> OnPostAcceptAsync(Guid id, string? status, CancellationToken cancellationToken) =>
        ActAsync(id, status, _bookings.AcceptAsync, cancellationToken);

    public Task<IActionResult> OnPostDeclineAsync(Guid id, string? status, CancellationToken cancellationToken) =>
        ActAsync(id, status, _bookings.DeclineAsync, cancellationToken);

    public Task<IActionResult> OnPostCompleteAsync(Guid id, string? status, CancellationToken cancellationToken) =>
        ActAsync(id, status, _bookings.CompleteAsync, cancellationToken);

    private async Task<IActionResult> ActAsync(
        Guid id,
        string? status,
        Func<Guid, CancellationToken, Task<ApiResult<BookingDto>>> action,
        CancellationToken cancellationToken)
    {
        var result = await action(id, cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            await LoadAsync(status, cancellationToken);
            return Page();
        }

        return RedirectToPage(new
        {
            status = BookingStatuses.TryNormalize(status, out var canonical) ? canonical : null,
            notice = InstructorNotices.ForStatus(result.Data.Status)
        });
    }

    private async Task LoadAsync(string? status, CancellationToken cancellationToken)
    {
        if (!BookingStatuses.TryNormalize(status, out var canonical))
        {
            Error ??= UiCopy.UnknownBookingStatus;
            Status = null;
            Bookings = [];
            return;
        }

        Status = canonical;
        var list = await _bookings.ListAsync(Status, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error ??= list.Error ?? UiCopy.GenericError;
            Bookings = [];
            return;
        }

        Bookings = list.Data;
    }
}

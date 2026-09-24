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
    public List<InstructorBookingCard> Cards { get; private set; } = [];
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
        var filter = BookingStatuses.TryNormalize(status, out var canonical) ? canonical : null;
        var result = await action(id, cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            var error = result.Error ?? UiCopy.GenericError;
            if (WantsFragment)
                return Fragment(new BookingMutationBody(false, null, error, null, false));

            Error = error;
            await LoadAsync(status, cancellationToken);
            return Page();
        }

        if (WantsFragment)
        {
            var booking = result.Data;
            var stays = InstructorInbox.StaysOnFilter(filter, booking.Status);
            return Fragment(new BookingMutationBody(
                true,
                InstructorNotices.Text(InstructorNotices.ForStatus(booking.Status)),
                null,
                stays ? new InstructorBookingCard(booking, filter) : null,
                stays));
        }

        return RedirectToPage(new
        {
            status = filter,
            notice = InstructorNotices.ForStatus(result.Data.Status)
        });
    }

    private PartialViewResult Fragment(BookingMutationBody body)
    {
        Response.Headers.CacheControl = "no-store";
        return Partial("_BookingMutation", body);
    }

    private bool WantsFragment =>
        string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.OrdinalIgnoreCase);

    private async Task LoadAsync(string? status, CancellationToken cancellationToken)
    {
        if (!BookingStatuses.TryNormalize(status, out var canonical))
        {
            Error ??= UiCopy.UnknownBookingStatus;
            Status = null;
            Bookings = [];
            Cards = [];
            return;
        }

        Status = canonical;
        var list = await _bookings.ListAsync(Status, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error ??= list.Error ?? UiCopy.GenericError;
            Bookings = [];
            Cards = [];
            return;
        }

        Bookings = list.Data;
        Cards = Bookings.Select(booking => new InstructorBookingCard(booking, Status)).ToList();
    }
}

public sealed record InstructorBookingCard(BookingDto Booking, string? FilterStatus);

public sealed record BookingMutationBody(
    bool Ok,
    string? Notice,
    string? Error,
    InstructorBookingCard? Card,
    bool Stays);

public static class InstructorInbox
{
    public static bool StaysOnFilter(string? filter, string status) =>
        filter is null || string.Equals(filter, status, StringComparison.Ordinal);
}

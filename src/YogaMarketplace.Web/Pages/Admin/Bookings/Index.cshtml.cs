using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin.Bookings;

public class BookingsModel : PageModel
{
    private readonly IAdminBookingApi _bookings;

    public BookingsModel(IAdminBookingApi bookings)
    {
        _bookings = bookings;
    }

    public List<AdminBookingDto> Bookings { get; private set; } = [];
    public string? Status { get; private set; }
    public bool StatusKnown { get; private set; } = true;
    public string? From { get; private set; }
    public string? To { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(string? status, string? from, string? to, CancellationToken cancellationToken)
    {
        From = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
        To = string.IsNullOrWhiteSpace(to) ? null : to.Trim();

        if (!BookingStatuses.TryNormalize(status, out var canonical))
        {
            StatusKnown = false;
            Error = UiCopy.UnknownBookingStatus;
            Bookings = [];
            return;
        }

        StatusKnown = true;
        Status = canonical;
        if (!AdminInput.TryDateRange(From, To, out var fromDate, out var toDate, out var dateError))
        {
            Error = dateError;
            Bookings = [];
            return;
        }

        var list = await _bookings.ListAsync(Status, fromDate, toDate, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error = list.Error ?? UiCopy.GenericError;
            Bookings = [];
            return;
        }

        Bookings = list.Data;
    }
}

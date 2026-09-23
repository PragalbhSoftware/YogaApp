using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin.Bookings;

public class BookingModel : PageModel
{
    private readonly IAdminBookingApi _bookings;

    public BookingModel(IAdminBookingApi bookings)
    {
        _bookings = bookings;
    }

    public AdminBookingDto? Booking { get; private set; }
    public string? Status { get; private set; }
    public string? From { get; private set; }
    public string? To { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(Guid id, string? status, string? from, string? to, CancellationToken cancellationToken)
    {
        Status = BookingStatuses.TryNormalize(status, out var canonical) ? canonical : null;
        From = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
        To = string.IsNullOrWhiteSpace(to) ? null : to.Trim();
        var result = await _bookings.GetAsync(id, cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            return;
        }

        Booking = result.Data;
    }
}

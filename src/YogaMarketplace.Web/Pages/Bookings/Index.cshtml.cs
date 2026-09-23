using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Bookings;

public class IndexModel : PageModel
{
    private readonly IBookingApi _bookings;

    public IndexModel(IBookingApi bookings)
    {
        _bookings = bookings;
    }

    public List<BookingDto> Bookings { get; private set; } = [];
    public string? Error { get; private set; }
    public bool Unreachable { get; private set; }
    public string? Notice { get; private set; }
    public Guid? JustBookedId { get; private set; }

    public async Task OnGetAsync(string? notice, Guid? booked, CancellationToken cancellationToken)
    {
        Notice = notice switch
        {
            CheckoutNotices.Abandoned => UiCopy.PaymentAbandoned,
            CheckoutNotices.Failed => UiCopy.PaymentFailed,
            _ => null
        };

        JustBookedId = booked;

        var mine = await _bookings.ListMineAsync(cancellationToken);
        if (!mine.Ok || mine.Data is null)
        {
            Error = mine.Error ?? UiCopy.GenericError;
            Unreachable = mine.Unreachable;
            Bookings = [];
            return;
        }

        Bookings = mine.Data;
    }
}

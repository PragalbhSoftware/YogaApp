using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Bookings;

public class IndexModel : PageModel
{
    private readonly IBookingApi _bookings;
    private readonly IReviewedBookingStore _reviewed;

    public IndexModel(IBookingApi bookings, IReviewedBookingStore reviewed)
    {
        _bookings = bookings;
        _reviewed = reviewed;
    }

    public List<BookingDto> Bookings { get; private set; } = [];
    public string? Error { get; private set; }
    public bool Unreachable { get; private set; }
    public string? Notice { get; private set; }
    public Guid? JustBookedId { get; private set; }

    public bool ShowReviewForm(BookingDto booking) =>
        CustomerReviews.CanSubmit(booking.Status, _reviewed.Contains(booking.Id));

    public bool ShowReviewedNote(BookingDto booking) =>
        booking.Status == BookingStatuses.Completed && _reviewed.Contains(booking.Id);

    public async Task OnGetAsync(string? notice, Guid? booked, CancellationToken cancellationToken)
    {
        Notice = notice switch
        {
            CheckoutNotices.Abandoned => UiCopy.PaymentAbandoned,
            CheckoutNotices.Failed => UiCopy.PaymentFailed,
            _ => null
        };

        JustBookedId = booked;
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostReviewAsync(Guid id, int rating, string? comment, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (!CustomerReviews.IsInRange(rating))
            Error = UiCopy.RatingRange;
        else if (text is { Length: > CustomerReviews.MaxCommentLength })
            Error = UiCopy.ReviewTooLong;
        else
        {
            var result = await _bookings.CreateReviewAsync(id, new CreateReviewDto { Rating = rating, Comment = text }, cancellationToken);
            if (result.Ok)
            {
                _reviewed.Remember(id);
                return RedirectToPage(new { reviewed = id });
            }

            if (result.StatusCode == StatusCodes.Status409Conflict)
                _reviewed.Remember(id);
            Error = result.Error ?? UiCopy.GenericError;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var mine = await _bookings.ListMineAsync(cancellationToken);
        if (!mine.Ok || mine.Data is null)
        {
            Error ??= mine.Error ?? UiCopy.GenericError;
            Unreachable = mine.Unreachable;
            Bookings = [];
            return;
        }

        Bookings = mine.Data;
    }
}

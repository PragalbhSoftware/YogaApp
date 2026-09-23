using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Bookings;

public class IndexModel : PageModel
{
    private readonly IBookingApi _bookings;
    private readonly IMarketplaceApi _catalog;

    public IndexModel(IBookingApi bookings, IMarketplaceApi catalog)
    {
        _bookings = bookings;
        _catalog = catalog;
    }

    public List<BookingDto> Bookings { get; private set; } = [];
    public Dictionary<Guid, IReadOnlyList<SlotDto>> RescheduleOptions { get; private set; } = [];
    public string? Error { get; private set; }
    public string? SlotsError { get; private set; }
    public bool Unreachable { get; private set; }
    public string? Notice { get; private set; }
    public string? Success { get; private set; }
    public Guid? JustBookedId { get; private set; }

    public bool ShowReviewForm(BookingDto booking) =>
        CustomerReviews.CanSubmit(booking.Status, booking.HasReviewed);

    public bool ShowReviewedNote(BookingDto booking) =>
        booking.Status == BookingStatuses.Completed && booking.HasReviewed;

    public IReadOnlyList<SlotDto> OptionsFor(Guid bookingId) =>
        RescheduleOptions.TryGetValue(bookingId, out var slots) ? slots : [];

    public async Task OnGetAsync(string? notice, Guid? booked, CancellationToken cancellationToken)
    {
        Notice = notice switch
        {
            CheckoutNotices.Abandoned => UiCopy.PaymentAbandoned,
            CheckoutNotices.Failed => UiCopy.PaymentFailed,
            _ => null
        };
        Success = CustomerNotices.Text(notice);
        JustBookedId = booked;
        await LoadAsync(cancellationToken);
    }

    public Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(_bookings.CancelAsync(id, cancellationToken), CustomerNotices.Cancelled, cancellationToken);

    public async Task<IActionResult> OnPostRescheduleAsync(Guid id, Guid slotId, CancellationToken cancellationToken)
    {
        if (slotId == Guid.Empty)
        {
            Error = UiCopy.RescheduleSlotRequired;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return await ChangeAsync(
            _bookings.RescheduleAsync(id, slotId, cancellationToken),
            CustomerNotices.Rescheduled,
            cancellationToken);
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
                return RedirectToPage();

            Error = result.Error ?? UiCopy.GenericError;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task<IActionResult> ChangeAsync(
        Task<ApiResult<BookingDto>> change,
        string notice,
        CancellationToken cancellationToken)
    {
        var result = await change;
        if (result.Ok)
            return RedirectToPage(new { notice });

        Error = result.Error
            ?? (result.StatusCode == StatusCodes.Status409Conflict ? UiCopy.SlotTaken : UiCopy.GenericError);
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
            RescheduleOptions = [];
            return;
        }

        Bookings = mine.Data;
        RescheduleOptions = await LoadRescheduleOptionsAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<SlotDto>>> LoadRescheduleOptionsAsync(CancellationToken cancellationToken)
    {
        var options = new Dictionary<Guid, IReadOnlyList<SlotDto>>();
        var cache = new Dictionary<string, ApiResult<SlotListDto>>(StringComparer.Ordinal);
        foreach (var booking in Bookings)
        {
            if (!CustomerBookingChanges.CanReschedule(booking))
                continue;

            var key = booking.ProviderId.ToString("N") + "|" + booking.Mode;
            if (!cache.TryGetValue(key, out var listed))
            {
                listed = await _catalog.GetSlotsAsync(booking.ProviderId, booking.Mode, cancellationToken);
                cache[key] = listed;
            }

            if (!listed.Ok || listed.Data?.Slots is null)
            {
                SlotsError ??= listed.Error ?? UiCopy.GenericError;
                options[booking.Id] = [];
                continue;
            }

            options[booking.Id] = RescheduleChoices.OpenFor(booking, listed.Data.Slots);
        }

        return options;
    }
}

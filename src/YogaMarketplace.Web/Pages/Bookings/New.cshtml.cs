using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Bookings;

public class NewModel : PageModel
{
    private readonly ISlotQuoteReader _quotes;
    private readonly IPaymentCheckout _checkout;

    public NewModel(ISlotQuoteReader quotes, IPaymentCheckout checkout)
    {
        _quotes = quotes;
        _checkout = checkout;
    }

    [BindProperty(SupportsGet = true)]
    public Guid ProviderId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid SlotId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    [BindProperty]
    public string? HomeAddress { get; set; }

    [BindProperty]
    public string? Landmark { get; set; }

    public SlotQuote? Quote { get; private set; }
    public CheckoutSummary? Summary => Quote is null ? null : CheckoutSummary.From(Quote);
    public string? Error { get; private set; }
    public bool Unreachable { get; private set; }
    public bool NeedsHomeAddress => Quote is not null && SessionModes.IsHome(Quote.Mode);

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken) || Quote is null)
            return Page();

        var started = await _checkout.BeginAsync(new BeginCheckout(
            Quote.ProviderId,
            Quote.SlotId,
            Quote.Mode,
            Quote.ProviderName,
            Quote.Date,
            Quote.Start,
            Quote.End,
            HomeAddress,
            Landmark,
            Quote.Area,
            Quote.City,
            Quote.StudioAddress), cancellationToken);

        if (!started.Ok || started.Data is null)
        {
            Error = started.Error ?? UiCopy.GenericError;
            Unreachable = started.Unreachable;
            return Page();
        }

        CheckoutDraftStore.Save(TempData, started.Data);
        return RedirectToPage("/Bookings/Pay");
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var quote = await _quotes.ReadAsync(ProviderId, SlotId, Mode, cancellationToken);
        if (!quote.Ok || quote.Data is null)
        {
            Error = quote.Error ?? UiCopy.SlotUnavailable;
            Unreachable = quote.Unreachable;
            Quote = null;
            return false;
        }

        Quote = quote.Data;
        Mode = quote.Data.Mode;
        return true;
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Bookings;

public class PayModel : PageModel
{
    private readonly IPaymentCheckout _checkout;

    public PayModel(IPaymentCheckout checkout)
    {
        _checkout = checkout;
    }

    [BindProperty]
    public string? OrderId { get; set; }

    [BindProperty]
    public string? PaymentId { get; set; }

    [BindProperty]
    public string? Signature { get; set; }

    public CheckoutDraft? Draft { get; private set; }
    public CheckoutSummary? Summary => Draft is null ? null : CheckoutSummary.From(Draft);
    public string? Error { get; private set; }
    public bool UseFakeCheckout => _checkout.UseFakeCheckout;

    public IActionResult OnGet()
    {
        Draft = CheckoutDraftStore.Peek(TempData);
        if (Draft is null)
            Error = UiCopy.CheckoutExpired;
        else
            OrderId = Draft.OrderId;
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(CancellationToken cancellationToken)
    {
        Draft = CheckoutDraftStore.Peek(TempData);
        if (Draft is null)
        {
            Error = UiCopy.CheckoutExpired;
            return Page();
        }

        var paid = await _checkout.CaptureLocalAsync(Draft, cancellationToken);
        if (!paid.Ok || paid.Data is null)
        {
            Error = paid.Error ?? UiCopy.GenericError;
            return Page();
        }

        return Booked(paid.Data);
    }

    public IActionResult OnPostFail() => Leave(CheckoutNotices.Failed);

    public IActionResult OnPostAbandon() => Leave(CheckoutNotices.Abandoned);

    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken cancellationToken)
    {
        Draft = CheckoutDraftStore.Peek(TempData);
        if (Draft is null)
        {
            Error = UiCopy.CheckoutExpired;
            return Page();
        }

        if (!string.Equals(OrderId?.Trim(), Draft.OrderId, StringComparison.Ordinal))
        {
            Error = UiCopy.PaymentIncomplete;
            return Page();
        }

        var paid = await _checkout.ConfirmGatewayAsync(OrderId, PaymentId, Signature, cancellationToken);
        if (!paid.Ok || paid.Data is null)
        {
            Error = paid.Error ?? UiCopy.PaymentFailed;
            return Page();
        }

        return Booked(paid.Data);
    }

    private IActionResult Leave(string notice)
    {
        CheckoutDraftStore.Clear(TempData);
        return RedirectToPage("/Bookings/Index", new { notice });
    }

    private IActionResult Booked(BookingDto booking)
    {
        CheckoutDraftStore.Clear(TempData);
        return RedirectToPage("/Bookings/Index", new { booked = booking.Id });
    }
}

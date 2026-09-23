using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin;

public class TransactionsModel : PageModel
{
    private readonly IAdminTransactionApi _transactions;

    public TransactionsModel(IAdminTransactionApi transactions)
    {
        _transactions = transactions;
    }

    public List<AdminPaymentDto> Payments { get; private set; } = [];
    public List<AdminPayoutDto> Payouts { get; private set; } = [];
    public string? PaymentStatus { get; private set; }
    public string PayoutStatus { get; private set; } = PayoutStatuses.Pending;
    public bool PaymentsLoaded { get; private set; }
    public bool PayoutsLoaded { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(string? payments, string? payouts, CancellationToken cancellationToken)
    {
        var paymentsOk = AdminPaymentStatuses.TryNormalize(payments, out var paymentStatus);
        var payoutsOk = PayoutStatuses.TryNormalize(payouts, out var payoutStatus);
        PaymentStatus = paymentsOk ? paymentStatus : null;
        PayoutStatus = payoutsOk ? payoutStatus : PayoutStatuses.Pending;

        if (!paymentsOk)
            Error = UiCopy.UnknownPaymentStatus;
        else
        {
            var list = await _transactions.PaymentsAsync(PaymentStatus, cancellationToken);
            if (!list.Ok || list.Data is null)
                Error = list.Error ?? UiCopy.GenericError;
            else
            {
                Payments = list.Data;
                PaymentsLoaded = true;
            }
        }

        if (!payoutsOk)
            Error = AdminInput.Join(Error, UiCopy.UnknownPayoutStatus);
        else
        {
            var list = await _transactions.PayoutsAsync(PayoutStatus, cancellationToken);
            if (!list.Ok || list.Data is null)
                Error = AdminInput.Join(Error, list.Error ?? UiCopy.GenericError);
            else
            {
                Payouts = list.Data;
                PayoutsLoaded = true;
            }
        }
    }
}

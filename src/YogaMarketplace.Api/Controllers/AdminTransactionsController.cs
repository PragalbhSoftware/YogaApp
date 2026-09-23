using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin")]
public class AdminTransactionsController : AdminControllerBase
{
    private readonly IAdminTransactionService _transactions;

    public AdminTransactionsController(IAdminTransactionService transactions)
    {
        _transactions = transactions;
    }

    [HttpGet("payments")]
    public async Task<ActionResult<IReadOnlyList<AdminPaymentResponse>>> Payments(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _transactions.ListPaymentsAsync(status, cancellationToken));

    [HttpGet("payouts")]
    public async Task<ActionResult<IReadOnlyList<AdminPayoutResponse>>> Payouts(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _transactions.ListPayoutsAsync(status, cancellationToken));
}

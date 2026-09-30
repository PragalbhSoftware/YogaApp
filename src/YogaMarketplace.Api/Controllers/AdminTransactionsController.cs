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

    [HttpGet("payouts/csv")]
    public async Task<IActionResult> PayoutsCsv([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var csv = await _transactions.DownloadPayoutsCsvAsync(status, cancellationToken);
        return File(csv.Content, "text/csv", csv.FileName);
    }

    [HttpPost("payouts/export")]
    public async Task<IActionResult> ExportPayouts(CancellationToken cancellationToken)
    {
        var csv = await _transactions.ExportPendingPayoutsAsync(cancellationToken);
        return File(csv.Content, "text/csv", csv.FileName);
    }

    [HttpPost("payouts/{id:guid}/paid")]
    public async Task<ActionResult<AdminPayoutResponse>> MarkPaid(Guid id, CancellationToken cancellationToken) =>
        Ok(await _transactions.MarkPaidAsync(id, cancellationToken));
}

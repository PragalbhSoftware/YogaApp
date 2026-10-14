using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/errors")]
public class AdminErrorsController : AdminControllerBase
{
    private readonly AdminErrorService _errors;

    public AdminErrorsController(AdminErrorService errors)
    {
        _errors = errors;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminErrorEventResponse>>> List(
        [FromQuery] bool? acknowledged,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _errors.ListAsync(acknowledged ?? false, page, pageSize, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<AdminErrorSummaryResponse>> Summary(CancellationToken cancellationToken) =>
        Ok(await _errors.SummaryAsync(cancellationToken));

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<AdminErrorEventResponse>> Acknowledge(Guid id, CancellationToken cancellationToken) =>
        Ok(await _errors.AcknowledgeAsync(id, cancellationToken));

    [HttpPost("test-alert")]
    public async Task<IActionResult> SendTestAlert(CancellationToken cancellationToken)
    {
        await _errors.SendTestAlertAsync(cancellationToken);
        return NoContent();
    }
}

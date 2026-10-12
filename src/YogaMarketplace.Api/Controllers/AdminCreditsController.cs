using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin")]
public class AdminCreditsController : AdminControllerBase
{
    private readonly IAdminCreditService _credits;

    public AdminCreditsController(IAdminCreditService credits)
    {
        _credits = credits;
    }

    [HttpGet("packs")]
    public async Task<ActionResult<IReadOnlyList<AdminPackResponse>>> Packs(CancellationToken cancellationToken) =>
        Ok(await _credits.ListPacksAsync(cancellationToken));

    [HttpPost("packs")]
    public async Task<ActionResult<AdminPackResponse>> CreatePack([FromBody] SavePackRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var pack = await _credits.CreatePackAsync(request, cancellationToken);
        return Created($"/api/admin/packs/{pack.Id}", pack);
    }

    [HttpPatch("packs/{id:guid}")]
    public async Task<ActionResult<AdminPackResponse>> UpdatePack(
        Guid id,
        [FromBody] SavePackRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _credits.UpdatePackAsync(id, request, cancellationToken));
    }

    [HttpGet("users/{id:guid}/credits")]
    public async Task<ActionResult<IReadOnlyList<AdminCreditBatchResponse>>> UserCredits(Guid id, CancellationToken cancellationToken) =>
        Ok(await _credits.ListForUserAsync(id, cancellationToken));

    [HttpPost("credit-batches/{id:guid}/refund")]
    public async Task<ActionResult<AdminCreditBatchResponse>> Refund(Guid id, CancellationToken cancellationToken) =>
        Ok(await _credits.RefundAsync(id, cancellationToken));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class PacksController : ControllerBase
{
    private readonly CreditPurchaseService _packs;

    public PacksController(CreditPurchaseService packs)
    {
        _packs = packs;
    }

    [HttpGet("packs")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Browse)]
    public async Task<ActionResult<IReadOnlyList<PackResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _packs.ListActiveAsync(cancellationToken));

    [HttpPost("packs/{id:guid}/orders")]
    public async Task<ActionResult<PackOrderResponse>> CreateOrder(Guid id, CancellationToken cancellationToken) =>
        Ok(await _packs.CreateOrderAsync(id, cancellationToken));

    [HttpPost("packs/confirm")]
    public async Task<ActionResult<PackOrderStatusResponse>> Confirm(
        [FromBody] ConfirmBookingPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _packs.ConfirmAsync(request, cancellationToken));
    }

    [HttpPost("packs/local-confirm")]
    public async Task<ActionResult<PackOrderStatusResponse>> ConfirmLocal(
        [FromBody] LocalConfirmRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _packs.ConfirmLocalAsync(request, cancellationToken));
    }

    [HttpGet("packs/orders/{orderId}")]
    public async Task<ActionResult<PackOrderStatusResponse>> OrderStatus(string orderId, CancellationToken cancellationToken) =>
        Ok(await _packs.StatusAsync(orderId, cancellationToken));

    [HttpGet("credits/me")]
    public async Task<ActionResult<MyCreditsResponse>> MyCredits(CancellationToken cancellationToken) =>
        Ok(await _packs.MineAsync(cancellationToken));
}

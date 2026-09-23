using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly ProviderService _providers;

    public ProvidersController(ProviderService providers)
    {
        _providers = providers;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProviderSummary>>> Browse(
        [FromQuery] string? area,
        [FromQuery] string? mode,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        Ok(await _providers.BrowseAsync(area, mode, category, cancellationToken));

    [Authorize]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterProviderResponse>> Register(
        [FromBody] RegisterProviderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var result = await _providers.RegisterAsync(request, cancellationToken);
        return Created($"/api/providers/{result.Provider.Id}", result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ProviderSelf>> Me(CancellationToken cancellationToken) =>
        Ok(await _providers.GetMineAsync(cancellationToken));

    [Authorize]
    [HttpGet("me/slots")]
    public async Task<ActionResult<OwnedSlotListResponse>> MySlots(
        [FromQuery] string? mode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _providers.GetMySlotsAsync(mode, from, to, cancellationToken));

    [Authorize]
    [HttpPost("me/slots")]
    public async Task<ActionResult<IReadOnlyList<SlotResponse>>> AddSlots(
        [FromBody] AddSlotsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var slots = await _providers.AddSlotsAsync(request, cancellationToken);
        return Ok(slots);
    }

    [Authorize]
    [HttpPost("me/slots/{id:guid}/block")]
    public async Task<ActionResult<OwnedSlotResponse>> BlockSlot(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.BlockSlotAsync(id, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProviderDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.GetPublicAsync(id, cancellationToken));

    [HttpGet("{id:guid}/slots")]
    public async Task<ActionResult<SlotListResponse>> Slots(
        Guid id,
        [FromQuery] string? mode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _providers.GetSlotsAsync(id, mode, from, to, cancellationToken));
}

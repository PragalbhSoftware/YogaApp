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
        [FromQuery] string? city,
        [FromQuery] string? area,
        [FromQuery] string? mode,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        Ok(await _providers.BrowseAsync(city, area, mode, category, cancellationToken));

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
    [HttpPatch("me")]
    public async Task<ActionResult<ProviderSelf>> UpdateProfile(
        [FromBody] UpdateProviderProfileRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _providers.UpdateProfileAsync(request, cancellationToken));
    }

    [Authorize]
    [HttpGet("me/payouts")]
    public async Task<ActionResult<IReadOnlyList<InstructorPayoutResponse>>> MyPayouts(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _providers.ListMyPayoutsAsync(status, cancellationToken));

    [Authorize]
    [HttpPatch("me/rates")]
    public async Task<ActionResult<ProviderSelf>> UpdateRates(
        [FromBody] UpdateProviderRatesRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _providers.UpdateRatesAsync(request, cancellationToken));
    }

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
    [HttpPut("me/slots/{id:guid}")]
    public async Task<ActionResult<OwnedSlotResponse>> UpdateSlot(
        Guid id,
        [FromBody] UpdateSlotRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _providers.UpdateSlotAsync(id, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("me/slots/{id:guid}/block")]
    public async Task<ActionResult<OwnedSlotResponse>> BlockSlot(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.BlockSlotAsync(id, cancellationToken));

    [Authorize]
    [HttpPost("me/slots/{id:guid}/unblock")]
    public async Task<ActionResult<OwnedSlotResponse>> UnblockSlot(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.UnblockSlotAsync(id, cancellationToken));

    [Authorize]
    [HttpDelete("me/slots/{id:guid}")]
    public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken cancellationToken)
    {
        await _providers.DeleteSlotAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProviderDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.GetPublicAsync(id, cancellationToken));

    [HttpGet("{id:guid}/reviews")]
    public async Task<ActionResult<IReadOnlyList<PublicReviewResponse>>> Reviews(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _providers.ListPublicReviewsAsync(id, cancellationToken));

    [HttpGet("{id:guid}/slots")]
    public async Task<ActionResult<SlotListResponse>> Slots(
        Guid id,
        [FromQuery] string? mode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _providers.GetSlotsAsync(id, mode, from, to, cancellationToken));
}

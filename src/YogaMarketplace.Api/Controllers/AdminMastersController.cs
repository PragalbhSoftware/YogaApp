using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin")]
public class AdminMastersController : AdminControllerBase
{
    private readonly IAdminMasterService _masters;

    public AdminMastersController(IAdminMasterService masters)
    {
        _masters = masters;
    }

    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AdminAreaResponse>>> Areas(CancellationToken cancellationToken) =>
        Ok(await _masters.ListAreasAsync(cancellationToken));

    [HttpPost("areas")]
    public async Task<ActionResult<AdminAreaResponse>> CreateArea(
        [FromBody] CreateAreaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var area = await _masters.CreateAreaAsync(request, cancellationToken);
        return Created($"/api/admin/areas/{area.Id}", area);
    }

    [HttpPatch("areas/{id:guid}")]
    public async Task<ActionResult<AdminAreaResponse>> UpdateArea(
        Guid id,
        [FromBody] PatchAreaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _masters.UpdateAreaAsync(id, request, cancellationToken));
    }

    [HttpGet("states")]
    public async Task<ActionResult<PagedResponse<AdminStateResponse>>> States(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _masters.ListStatesAsync(page, pageSize, cancellationToken));

    [HttpPost("states")]
    public async Task<ActionResult<AdminStateResponse>> CreateState(
        [FromBody] CreateStateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var state = await _masters.CreateStateAsync(request, cancellationToken);
        return Created($"/api/admin/states/{state.Id}", state);
    }

    [HttpPost("states/import-india")]
    public async Task<ActionResult<LocationImportResponse>> ImportIndia(CancellationToken cancellationToken) =>
        Ok(await _masters.ImportIndiaLocationsAsync(cancellationToken));

    [HttpPatch("states/{id:guid}")]
    public async Task<ActionResult<AdminStateResponse>> UpdateState(
        Guid id,
        [FromBody] PatchStateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _masters.UpdateStateAsync(id, request, cancellationToken));
    }

    [HttpGet("cities")]
    public async Task<ActionResult<PagedResponse<AdminCityResponse>>> Cities(
        [FromQuery] Guid? stateId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _masters.ListCitiesAsync(stateId, page, pageSize, cancellationToken));

    [HttpPost("cities")]
    public async Task<ActionResult<AdminCityResponse>> CreateCity(
        [FromBody] CreateCityRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var city = await _masters.CreateCityAsync(request, cancellationToken);
        return Created($"/api/admin/cities/{city.Id}", city);
    }

    [HttpPatch("cities/{id:guid}")]
    public async Task<ActionResult<AdminCityResponse>> UpdateCity(
        Guid id,
        [FromBody] PatchCityRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _masters.UpdateCityAsync(id, request, cancellationToken));
    }

    [HttpDelete("cities/{id:guid}")]
    public async Task<IActionResult> DeleteCity(Guid id, CancellationToken cancellationToken)
    {
        await _masters.DeleteCityAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryResponse>>> Categories(CancellationToken cancellationToken) =>
        Ok(await _masters.ListCategoriesAsync(cancellationToken));

    [HttpPatch("categories/{id:guid}")]
    public async Task<ActionResult<AdminCategoryResponse>> UpdateCategory(
        Guid id,
        [FromBody] PatchCategoryRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _masters.UpdateCategoryAsync(id, request, cancellationToken));
    }
}

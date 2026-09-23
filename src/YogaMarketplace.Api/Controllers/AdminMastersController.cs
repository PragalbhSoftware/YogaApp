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

    [HttpGet("policy")]
    public async Task<ActionResult<PolicyResponse>> Policy(CancellationToken cancellationToken) =>
        Ok(await _masters.GetPolicyAsync(cancellationToken));

    [HttpPatch("policy")]
    public async Task<ActionResult<PolicyResponse>> UpdatePolicy(
        [FromBody] PatchPolicyRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _masters.UpdatePolicyAsync(request, cancellationToken));
    }
}

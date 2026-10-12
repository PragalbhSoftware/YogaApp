using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/pages")]
public class AdminPagesController : AdminControllerBase
{
    private readonly ContentPageService _pages;

    public AdminPagesController(ContentPageService pages)
    {
        _pages = pages;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContentPageResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _pages.ListAsync(cancellationToken));

    [HttpPut("{slug}")]
    public async Task<ActionResult<ContentPageResponse>> Update(
        string slug,
        [FromBody] UpdateContentPageRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _pages.UpdateAsync(slug, request, cancellationToken));
    }
}

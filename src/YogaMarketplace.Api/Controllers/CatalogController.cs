using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Route("api")]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalog;

    public CatalogController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AreaResponse>>> Areas(CancellationToken cancellationToken) =>
        Ok(await _catalog.AreasAsync(cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> Categories(CancellationToken cancellationToken) =>
        Ok(await _catalog.CategoriesAsync(cancellationToken));

    [HttpGet("policy")]
    public async Task<ActionResult<PolicyResponse>> Policy(CancellationToken cancellationToken) =>
        Ok(await _catalog.PolicyAsync(cancellationToken));
}

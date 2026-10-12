using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Route("api/pages")]
[EnableRateLimiting(RateLimitPolicies.Browse)]
public class PagesController : ControllerBase
{
    private readonly ContentPageService _pages;

    public PagesController(ContentPageService pages)
    {
        _pages = pages;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ContentPageResponse>> Get(string slug, CancellationToken cancellationToken) =>
        Ok(await _pages.GetAsync(slug, cancellationToken));
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

/// <summary>Public instructor photos. Certificates are never served here.</summary>
[ApiController]
[Route("api/media")]
[EnableRateLimiting(RateLimitPolicies.Browse)]
public class MediaController : ControllerBase
{
    private const int PhotoCacheSeconds = 60 * 60 * 24 * 7;

    private readonly ProviderMediaService _media;

    public MediaController(ProviderMediaService media)
    {
        _media = media;
    }

    [HttpGet("photos/{id:guid}")]
    [ResponseCache(Duration = PhotoCacheSeconds, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        var file = await _media.OpenPublicPhotoAsync(id, cancellationToken);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/providers/me")]
public class ProviderMediaController : ControllerBase
{
    private const long MultipartOverheadBytes = 1024 * 1024;
    private const long PhotoRequestLimit = ProviderMediaRules.MaxPhotoBytes + MultipartOverheadBytes;
    private const long CertificateRequestLimit = ProviderMediaRules.MaxCertificateBytes + MultipartOverheadBytes;

    private readonly ProviderMediaService _media;

    public ProviderMediaController(ProviderMediaService media)
    {
        _media = media;
    }

    [HttpGet("media")]
    public async Task<ActionResult<ProviderMediaResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await _media.GetMineAsync(cancellationToken));

    [HttpPost("photos")]
    [RequestSizeLimit(PhotoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoRequestLimit)]
    public async Task<ActionResult<ProviderPhotoResponse>> AddPhoto(IFormFile? file, CancellationToken cancellationToken) =>
        Ok(await _media.AddPhotoAsync(file, cancellationToken));

    [HttpDelete("photos/{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        await _media.DeletePhotoAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("intro-video")]
    public async Task<ActionResult<ProviderMediaResponse>> SetIntroVideo(
        [FromBody] IntroVideoRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _media.SetIntroVideoAsync(request.Url, cancellationToken));

    [HttpPost("certificates")]
    [RequestSizeLimit(CertificateRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = CertificateRequestLimit)]
    public async Task<ActionResult<ProviderCertificateResponse>> AddCertificate(
        [FromForm] string? title,
        IFormFile? file,
        CancellationToken cancellationToken) =>
        Ok(await _media.AddCertificateAsync(title, file, cancellationToken));

    [HttpDelete("certificates/{id:guid}")]
    public async Task<IActionResult> DeleteCertificate(Guid id, CancellationToken cancellationToken)
    {
        await _media.DeleteCertificateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("certificates/{id:guid}/file")]
    public async Task<IActionResult> CertificateFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await _media.OpenMyCertificateAsync(id, cancellationToken);
        return PrivateFile(this, file);
    }

    internal static FileStreamResult PrivateFile(ControllerBase controller, StoredFile file)
    {
        controller.Response.Headers.CacheControl = "private, no-store";
        controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return controller.File(file.Content, file.ContentType, file.FileName);
    }
}

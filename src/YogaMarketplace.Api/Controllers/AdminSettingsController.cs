using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/settings")]
public class AdminSettingsController : AdminControllerBase
{
    private readonly IPlatformSettingsService _settings;

    public AdminSettingsController(IPlatformSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<ActionResult<AdminSettingsResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetForAdminAsync(cancellationToken));

    [HttpPatch]
    public async Task<ActionResult<AdminSettingsResponse>> Update(
        [FromBody] UpdateSettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _settings.UpdateAsync(request, cancellationToken));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<SettingsAuditResponse>>> Audit(CancellationToken cancellationToken) =>
        Ok(await _settings.ListAuditAsync(cancellationToken));
}

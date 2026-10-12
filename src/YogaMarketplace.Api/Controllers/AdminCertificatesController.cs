using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/certificates")]
public class AdminCertificatesController : AdminControllerBase
{
    private readonly IAdminCertificateService _certificates;

    public AdminCertificatesController(IAdminCertificateService certificates)
    {
        _certificates = certificates;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCertificateResponse>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _certificates.ListAsync(status, cancellationToken));

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> CertificateFile(Guid id, CancellationToken cancellationToken) =>
        ProviderMediaController.PrivateFile(this, await _certificates.OpenFileAsync(id, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdminCertificateResponse>> Approve(Guid id, CancellationToken cancellationToken) =>
        Ok(await _certificates.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AdminCertificateResponse>> Reject(
        Guid id,
        [FromBody] RejectCertificateRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _certificates.RejectAsync(id, request?.Reason, cancellationToken));
}

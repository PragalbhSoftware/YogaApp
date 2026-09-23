using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/providers")]
public class AdminProvidersController : AdminControllerBase
{
    private readonly IAdminProviderService _providers;

    public AdminProvidersController(IAdminProviderService providers)
    {
        _providers = providers;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProviderResponse>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _providers.ListAsync(status, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminProviderResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<AdminProviderResponse>> Verify(Guid id, CancellationToken cancellationToken) =>
        Ok(await _providers.VerifyAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AdminProviderResponse>> Reject(
        Guid id,
        [FromBody] RejectProviderRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _providers.RejectAsync(id, request?.Reason, cancellationToken));
}

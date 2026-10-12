using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/providers/me")]
public class ProviderTrialController : ControllerBase
{
    private readonly ProviderTrialService _trials;

    public ProviderTrialController(ProviderTrialService trials)
    {
        _trials = trials;
    }

    [HttpGet("services")]
    public async Task<ActionResult<IReadOnlyList<ProviderServiceResponse>>> Services(CancellationToken cancellationToken) =>
        Ok(await _trials.ListServicesAsync(cancellationToken));

    [HttpGet("trial")]
    public async Task<ActionResult<ProviderTrialResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await _trials.GetAsync(cancellationToken));

    [HttpPut("trial")]
    public async Task<ActionResult<ProviderTrialResponse>> Update(
        [FromBody] UpdateProviderTrialRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _trials.UpdateAsync(request, cancellationToken));
}

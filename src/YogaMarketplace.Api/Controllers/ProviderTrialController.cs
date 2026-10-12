using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/providers/me/trial")]
public class ProviderTrialController : ControllerBase
{
    private readonly ProviderTrialService _trials;

    public ProviderTrialController(ProviderTrialService trials)
    {
        _trials = trials;
    }

    [HttpGet]
    public async Task<ActionResult<ProviderTrialResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await _trials.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ProviderTrialResponse>> Update(
        [FromBody] UpdateProviderTrialRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _trials.UpdateAsync(request, cancellationToken));
}

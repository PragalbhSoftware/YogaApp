using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profile;

    public ProfileController(ProfileService profile)
    {
        _profile = profile;
    }

    [HttpGet]
    public async Task<ActionResult<CustomerProfileResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await _profile.GetMineAsync(cancellationToken));

    [HttpPatch]
    public async Task<ActionResult<CustomerProfileResponse>> Update(
        [FromBody] UpdateCustomerProfileRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _profile.UpdateAccountAsync(request, cancellationToken));
    }

    [HttpPut("visit-address")]
    public async Task<ActionResult<CustomerProfileResponse>> UpdateVisitAddress(
        [FromBody] UpdateVisitAddressRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        return Ok(await _profile.UpdateVisitAddressAsync(request, cancellationToken));
    }
}

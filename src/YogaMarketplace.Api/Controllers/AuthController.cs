using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly OtpService _otp;
    private readonly JwtTokenService _tokens;
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly RefreshTokenService _refreshTokens;
    private readonly RefreshCookie _refreshCookie;

    public AuthController(
        OtpService otp,
        JwtTokenService tokens,
        YogaDbContext db,
        ICurrentUser current,
        RefreshTokenService refreshTokens,
        RefreshCookie refreshCookie)
    {
        _otp = otp;
        _tokens = tokens;
        _db = db;
        _current = current;
        _refreshTokens = refreshTokens;
        _refreshCookie = refreshCookie;
    }

    [HttpPost("otp/request")]
    public async Task<ActionResult<OtpResponse>> RequestOtp([FromBody] RequestOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var result = await _otp.RequestAsync(request, cancellationToken);
        return Ok(new OtpResponse(result.ChallengeId, result.ExpiresAt, result.DevCode));
    }

    [HttpPost("otp/resend")]
    public async Task<ActionResult<OtpResponse>> Resend([FromBody] ResendOtpRequest? request, CancellationToken cancellationToken)
    {
        var result = await _otp.ResendAsync(request?.Phone, cancellationToken);
        return Ok(new OtpResponse(result.ChallengeId, result.ExpiresAt, result.DevCode));
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<VerifyResponse>> Verify([FromBody] VerifyOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Invalid request." });
        var user = await _otp.VerifyAsync(request, cancellationToken);
        _refreshCookie.Write(Response, await _refreshTokens.IssueAsync(user, cancellationToken));
        return Ok(new VerifyResponse(_tokens.Create(user), ToResponse(user)));
    }

    /// <summary>Swaps the refresh cookie for a new one and returns a fresh access token.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<VerifyResponse>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var (user, refresh) = await _refreshTokens.RotateAsync(_refreshCookie.Read(Request), cancellationToken);
            _refreshCookie.Write(Response, refresh);
            return Ok(new VerifyResponse(_tokens.Create(user), ToResponse(user)));
        }
        catch (DomainException ex) when (ex.StatusCode != StatusCodes.Status409Conflict)
        {
            // On 409 another request already set a newer cookie; clearing it would sign that tab out.
            _refreshCookie.Clear(Response);
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _refreshTokens.SignOutAsync(_refreshCookie.Read(Request), cancellationToken);
        _refreshCookie.Clear(Response);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == _current.UserId, cancellationToken);
        if (user is null)
            return Unauthorized();
        return Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Name, user.Phone, user.Gender?.ToString(), user.Role.ToString());
}

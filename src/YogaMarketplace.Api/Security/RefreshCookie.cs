using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;

namespace YogaMarketplace.Api.Security;

/// <summary>The refresh token travels only in this httpOnly cookie, scoped to the auth routes.</summary>
public class RefreshCookie
{
    private readonly RefreshTokenOptions _options;

    public RefreshCookie(IOptions<RefreshTokenOptions> options)
    {
        _options = options.Value;
    }

    public string? Read(HttpRequest request) => request.Cookies[RefreshTokenOptions.CookieName];

    public void Write(HttpResponse response, IssuedRefreshToken token) =>
        response.Cookies.Append(RefreshTokenOptions.CookieName, token.Value, Options(token.ExpiresAt));

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(RefreshTokenOptions.CookieName, Options(null));

    private CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = _options.CookieSecure,
        SameSite = Enum.TryParse<SameSiteMode>(_options.CookieSameSite, ignoreCase: true, out var mode) ? mode : SameSiteMode.Strict,
        Path = RefreshTokenOptions.CookiePath,
        Expires = expires,
        IsEssential = true
    };
}

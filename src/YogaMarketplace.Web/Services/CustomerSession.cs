using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace YogaMarketplace.Web.Services;

public static class AuthSession
{
    public const string ApiTokenClaim = "api_token";
    public const string AreaCookie = "ym.area";

    public static async Task SignInAsync(HttpContext http, VerifyResponseDto auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.Id.ToString()),
            new(ClaimTypes.MobilePhone, auth.User.Phone),
            new(ClaimTypes.Role, auth.User.Role),
            new(ApiTokenClaim, auth.Token)
        };
        if (!string.IsNullOrWhiteSpace(auth.User.Name))
            claims.Add(new Claim(ClaimTypes.Name, auth.User.Name));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}

public static class AreaCookie
{
    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthSession.AreaCookie, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public static void Write(HttpContext http, string area)
    {
        http.Response.Cookies.Append(AuthSession.AreaCookie, area, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(30),
            Path = "/"
        });
    }
}

public static class ProviderStatuses
{
    public const string Verified = "Verified";
}

public static class SessionModes
{
    public const string Home = "Home";
    public const string Studio = "Studio";
    public const string Online = "Online";

    public static readonly string[] All = [Home, Studio, Online];

    public static bool IsHome(string? mode) =>
        string.Equals(Normalize(mode), Home, StringComparison.Ordinal);

    public static string? Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return null;
        return All.FirstOrDefault(item => item.Equals(mode.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class CustomerFlow
{
    public static IActionResult AfterSignIn(HttpContext http, string? returnUrl, IUrlHelper url)
    {
        if (!string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl))
            return new RedirectResult(returnUrl);
        if (string.IsNullOrWhiteSpace(AreaCookie.Read(http.Request)))
            return new RedirectToPageResult("/Areas/Select");
        return new RedirectToPageResult("/Instructors/Index");
    }
}

public static class Money
{
    public static string Inr(decimal rate) => "₹" + rate.ToString("0.##", CultureInfo.InvariantCulture);
}

public static class TextTrim
{
    public static string Brief(string? value, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var text = value.Trim();
        if (text.Length <= max)
            return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }
}

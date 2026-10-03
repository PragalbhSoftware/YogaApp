namespace YogaMarketplace.Api.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "YogaMarketplace";
    public string Audience { get; set; } = "YogaMarketplace";
    public string Key { get; set; } = "";
    /// <summary>Access-token lifetime. Kept short; the client refreshes silently.</summary>
    public int ExpiresMinutes { get; set; } = 15;
}

public class RefreshTokenOptions
{
    public const string Section = "RefreshToken";
    public const string CookieName = "ym_refresh";
    public const string CookiePath = "/api/auth";
    public int LifetimeDays { get; set; } = 30;
    /// <summary>How long a just-rotated token is treated as a tab race rather than theft.</summary>
    public int ReuseGraceSeconds { get; set; } = 30;
    /// <summary>Must be true wherever the API is served over HTTPS (Production).</summary>
    public bool CookieSecure { get; set; } = true;
    /// <summary>Strict works for same-site setups. Use None only for a cross-site API, with CookieSecure.</summary>
    public string CookieSameSite { get; set; } = "Strict";
}

public class OtpOptions
{
    public const string Section = "Otp";
    public bool ExposeCode { get; set; }
    public bool UseFixedCode { get; set; }
    public string FixedCode { get; set; } = "123456";
    public string Pepper { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
}

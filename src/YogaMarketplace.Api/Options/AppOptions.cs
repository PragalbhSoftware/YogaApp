namespace YogaMarketplace.Api.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "YogaMarketplace";
    public string Audience { get; set; } = "YogaMarketplace";
    public string Key { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 10080;
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

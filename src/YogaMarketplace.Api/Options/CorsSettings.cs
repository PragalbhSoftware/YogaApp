namespace YogaMarketplace.Api.Options;

public class CorsSettings
{
    public const string Section = "Cors";
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Trimmed origins without a trailing slash. Production must list at least one, so a missing
    /// setting fails at startup instead of silently blocking or opening the SPA.
    /// </summary>
    public static string[] AllowedOriginsFrom(IConfiguration configuration, bool isProduction)
    {
        var settings = configuration.GetSection(Section).Get<CorsSettings>() ?? new CorsSettings();
        var origins = settings.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (isProduction && origins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins must list the SPA origin in Production.");
        return origins;
    }
}

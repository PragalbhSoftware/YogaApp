using Microsoft.AspNetCore.DataProtection;

namespace YogaMarketplace.Api.Options;

/// <summary>
/// Keeps the data protection key ring on disk so anything it protects stays readable after an IIS app pool
/// recycle or a redeploy. The folder must be outside the site folder, which deploys overwrite.
/// Env: DataProtection__KeysPath.
/// </summary>
public static class DataProtectionSettings
{
    public const string KeysPathKey = "DataProtection:KeysPath";
    public const string ApplicationName = "YogaMarketplace";

    public static IServiceCollection AddPersistedDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName(ApplicationName);
        var keysPath = configuration[KeysPathKey];
        if (!string.IsNullOrWhiteSpace(keysPath))
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath.Trim()));
        return services;
    }

    /// <summary>Null when the path is usable in Production; otherwise the startup problem.</summary>
    public static string? ProductionProblem(IConfiguration configuration)
    {
        var keysPath = configuration[KeysPathKey];
        if (string.IsNullOrWhiteSpace(keysPath))
            return "DataProtection:KeysPath is required in Production. Use a folder outside the site folder.";
        return Path.IsPathFullyQualified(keysPath.Trim())
            ? null
            : "DataProtection:KeysPath must be a full path outside the site folder.";
    }
}

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace YogaMarketplace.Api.Options;

/// <summary>
/// Trusts X-Forwarded-For/Proto only from the proxies listed in config, so rate limits and logs see the real client IP.
/// Env: ForwardedHeaders__KnownProxies__0=10.0.0.4, ForwardedHeaders__KnownNetworks__0=10.0.0.0/24.
/// </summary>
public static class ForwardedHeadersSettings
{
    public const string Section = "ForwardedHeaders";

    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(Section);
        var proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in proxies)
            {
                if (!IPAddress.TryParse(proxy, out var address))
                    throw new InvalidOperationException($"ForwardedHeaders:KnownProxies has an invalid IP address: '{proxy}'.");
                options.KnownProxies.Add(address);
            }
            foreach (var network in networks)
                options.KnownNetworks.Add(ParseNetwork(network));
        });
        return services;
    }

    private static IPNetwork ParseNetwork(string value)
    {
        var parts = value.Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix) || !int.TryParse(parts[1], out var length))
            throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks has an invalid CIDR range: '{value}'.");
        return new IPNetwork(prefix, length);
    }
}

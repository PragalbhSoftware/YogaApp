using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;

namespace YogaMarketplace.Api.Tests;

public class ForwardedHeadersSettingsTests
{
    [Fact]
    public void A_canonical_known_network_is_registered()
    {
        var options = ResolveOptions("10.0.0.0/24");
        Assert.Contains(System.Net.IPNetwork.Parse("10.0.0.0/24"), options.KnownIPNetworks);
    }

    [Fact]
    public void A_known_network_with_host_bits_set_fails_when_options_are_read()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _ = ResolveOptions("10.0.0.4/24"));
        Assert.Contains("10.0.0.4/24", error.Message);
    }

    private static ForwardedHeadersOptions ResolveOptions(string cidr)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = cidr
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTrustedProxies(config);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class TimeProviderTests
{
    [Fact]
    public async Task App_timestamps_come_from_the_injected_clock()
    {
        await using var factory = new YogaApiFactory();
        // Ahead of the real clock, so the sign-in token it stamps is still valid.
        var clock = new TestClock(new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(3), TimeSpan.Zero));
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
        var admin = host.CreateClient();
        var otp = JsonNode.Parse(await (await admin.PostAsJsonAsync("/api/auth/otp/request", new { phone = SeedIds.AdminPhone })).Content.ReadAsStringAsync())!;
        var verified = await admin.PostAsJsonAsync("/api/auth/otp/verify", new { phone = SeedIds.AdminPhone, code = otp["devCode"]!.GetValue<string>() });
        var token = JsonNode.Parse(await verified.Content.ReadAsStringAsync())!["token"]!.GetValue<string>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var saved = await AdminSettingsClient.PatchAsync(admin, version => new { version, policyNote = "Clock check" });

        Assert.Equal(clock.Now, saved.UpdatedAt);
    }
}

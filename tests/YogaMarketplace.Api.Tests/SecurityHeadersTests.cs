using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Tests;

public class SecurityHeadersTests : IClassFixture<YogaApiFactory>
{
    private readonly YogaApiFactory _factory;

    public SecurityHeadersTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/admin/providers")]
    public async Task Every_response_is_hardened_and_not_cached(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("+919876543210", "*********3210")]
    [InlineData("123", "***")]
    public void Otp_log_keeps_only_the_last_four_digits(string phone, string expected)
    {
        Assert.Equal(expected, LoggingOtpSender.MaskPhone(phone));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";
}

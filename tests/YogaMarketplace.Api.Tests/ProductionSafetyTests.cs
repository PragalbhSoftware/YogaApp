using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Middleware;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Tests;

public class ProductionSafetyTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public ProductionSafetyTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly Dictionary<string, string?> SafeProduction = new()
    {
        ["Database:AutoMigrate"] = "false",
        ["Otp:ExposeCode"] = "false",
        ["Otp:UseFixedCode"] = "false",
        ["Razorpay:KeyId"] = "rzp_live_example",
        ["Razorpay:KeySecret"] = "live-secret",
        ["Razorpay:WebhookSecret"] = "webhook-secret",
        ["Razorpay:UseFakeGateway"] = "false",
        ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "yoga-keys")
    };

    [Fact]
    public void Safe_production_settings_start_without_warnings()
    {
        Assert.Empty(ProductionSafety.Check(Config(), isProduction: true));
    }

    [Theory]
    [InlineData("Database:AutoMigrate", "true", "AutoMigrate")]
    [InlineData("Otp:ExposeCode", "true", "Otp:ExposeCode")]
    [InlineData("Otp:UseFixedCode", "true", "Otp:ExposeCode")]
    [InlineData("Razorpay:UseFakeGateway", "true", "UseFakeGateway")]
    [InlineData("Razorpay:KeyId", "", "Razorpay:KeyId")]
    [InlineData("Razorpay:KeySecret", " ", "Razorpay:KeyId")]
    [InlineData("Razorpay:WebhookSecret", "", "Razorpay:KeyId")]
    [InlineData("Otp:DemoCode", "246810", "Otp:DemoCode is set without Otp:DemoPhones")]
    [InlineData("DataProtection:KeysPath", "", "DataProtection:KeysPath is required")]
    [InlineData("DataProtection:KeysPath", "App_Data/keys", "DataProtection:KeysPath must be a full path")]
    public void Unsafe_production_settings_stop_startup(string key, string value, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config((key, value)), isProduction: true));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Demo_mode_with_phones_and_a_code_is_allowed_in_production()
    {
        Assert.Empty(ProductionSafety.Check(
            Config(("Otp:DemoPhones", "+919811100001,+919811100002"), ("Otp:DemoCode", "246810")), isProduction: true));
    }

    [Theory]
    [InlineData("+919811100001", "12ab56", "Otp:DemoCode must be 6 digits")]
    [InlineData("+919811100001,not-a-phone", "246810", "Otp:DemoPhones has an entry")]
    public void A_malformed_demo_setup_stops_startup(string phones, string code, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config(("Otp:DemoPhones", phones), ("Otp:DemoCode", code)), isProduction: true));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Test_keys_in_production_only_warn()
    {
        var warning = Assert.Single(ProductionSafety.Check(Config(("Razorpay:KeyId", "rzp_test_abc")), isProduction: true));
        Assert.Contains("test keys", warning);
    }

    [Fact]
    public void Other_environments_are_not_checked()
    {
        Assert.Empty(ProductionSafety.Check(Config(("Database:AutoMigrate", "true"), ("Razorpay:KeyId", "")), isProduction: false));
    }

    [Fact]
    public async Task Unknown_api_routes_return_a_json_error()
    {
        var response = await _factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);
        Assert.Equal("Not found.", body!.Error);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task Unhandled_errors_return_a_generic_message_and_a_trace_id()
    {
        using var broken = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ContentPageService>(_ => throw new InvalidOperationException("secret connection detail"))));

        var response = await broken.CreateClient().GetAsync("/api/pages/terms");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret connection detail", raw);
        var body = JsonSerializer.Deserialize<ErrorBody>(raw, Json);
        Assert.Equal(ApiErrorResponses.UnexpectedMessage, body!.Error);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static IConfiguration Config(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>(SafeProduction);
        foreach (var (key, value) in overrides)
            values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed record ErrorBody(string Error, string? TraceId);
}

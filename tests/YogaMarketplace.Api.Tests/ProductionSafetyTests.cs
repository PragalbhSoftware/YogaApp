using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    private static readonly (string, string)[] StagingKeys = [("Razorpay:KeyId", "rzp_test_example")];
    private static readonly (string, string)[] DemoSignIn = [("Otp:DemoPhones", "+919811100001,+919811100002"), ("Otp:DemoCode", "246810")];

    [Fact]
    public void Safe_production_settings_start_without_warnings()
    {
        Assert.Empty(ProductionSafety.Check(Config(), Environments.Production));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void Staging_gets_production_hardening(string environment, bool expected)
    {
        Assert.Equal(expected, ProductionSafety.AppliesTo(environment));
    }

    [Fact]
    public void Staging_with_test_keys_starts_without_warnings()
    {
        Assert.Empty(ProductionSafety.Check(Config(StagingKeys), Environments.Staging));
    }

    [Fact]
    public void Staging_with_live_keys_stops_startup()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ProductionSafety.Check(Config(), Environments.Staging));
        Assert.Contains("must be a test key", error.Message);
    }

    [Theory]
    [InlineData("Database:AutoMigrate", "true", "AutoMigrate")]
    [InlineData("Otp:ExposeCode", "true", "Otp:ExposeCode")]
    [InlineData("Razorpay:UseFakeGateway", "true", "UseFakeGateway")]
    [InlineData("Razorpay:KeySecret", "", "Razorpay:KeyId")]
    [InlineData("DataProtection:KeysPath", "", "DataProtection:KeysPath is required")]
    public void Unsafe_staging_settings_stop_startup(string key, string value, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config([.. StagingKeys, (key, value)]), Environments.Staging));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Demo_sign_in_is_allowed_on_staging()
    {
        Assert.Empty(ProductionSafety.Check(Config([.. StagingKeys, .. DemoSignIn]), Environments.Staging));
    }

    [Theory]
    [InlineData("+919811100001,+919811100002", "246810")]
    [InlineData("+919811100001", "")]
    [InlineData("", "246810")]
    public void Demo_sign_in_stops_startup_in_production(string phones, string code)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config(("Otp:DemoPhones", phones), ("Otp:DemoCode", code)), Environments.Production));
        Assert.Contains("must be empty in Production", error.Message);
    }

    [Theory]
    [InlineData("Database:AutoMigrate", "true", "AutoMigrate")]
    [InlineData("Otp:ExposeCode", "true", "Otp:ExposeCode")]
    [InlineData("Otp:UseFixedCode", "true", "Otp:ExposeCode")]
    [InlineData("Razorpay:UseFakeGateway", "true", "UseFakeGateway")]
    [InlineData("Razorpay:KeyId", "", "Razorpay:KeyId")]
    [InlineData("Razorpay:KeySecret", " ", "Razorpay:KeyId")]
    [InlineData("Razorpay:WebhookSecret", "", "Razorpay:KeyId")]
    [InlineData("DataProtection:KeysPath", "", "DataProtection:KeysPath is required")]
    [InlineData("DataProtection:KeysPath", "App_Data/keys", "DataProtection:KeysPath must be a full path")]
    public void Unsafe_production_settings_stop_startup(string key, string value, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config((key, value)), Environments.Production));
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("", "246810", "Otp:DemoCode is set without Otp:DemoPhones")]
    [InlineData("+919811100001", "12ab56", "Otp:DemoCode must be 6 digits")]
    [InlineData("+919811100001,not-a-phone", "246810", "Otp:DemoPhones has an entry")]
    public void A_malformed_demo_setup_stops_staging_startup(string phones, string code, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionSafety.Check(Config([.. StagingKeys, ("Otp:DemoPhones", phones), ("Otp:DemoCode", code)]), Environments.Staging));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Test_keys_in_production_only_warn()
    {
        var warning = Assert.Single(ProductionSafety.Check(Config(("Razorpay:KeyId", "rzp_test_abc")), Environments.Production));
        Assert.Contains("test keys", warning);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Other_environments_are_not_checked(string environment)
    {
        Assert.Empty(ProductionSafety.Check(
            Config([("Database:AutoMigrate", "true"), ("Razorpay:KeyId", ""), .. DemoSignIn]), environment));
    }

    [Theory]
    [InlineData("Production", "Razorpay:UseFakeGateway")]
    [InlineData("Production", "Otp:ExposeCode")]
    [InlineData("Production", "Otp:UseFixedCode")]
    [InlineData("Staging", "Razorpay:UseFakeGateway")]
    [InlineData("Staging", "Otp:ExposeCode")]
    [InlineData("Staging", "Otp:UseFixedCode")]
    public void Hardened_host_refuses_to_start_with_a_test_flag_on(string environment, string flag)
    {
        var logs = new LogCapture();
        using var host = HostIn(environment, logs, (flag, "true"));

        var error = Record.Exception(() => host.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(flag, error.Message);
        var critical = Assert.Single(logs.Lines, line => line.StartsWith("Critical", StringComparison.Ordinal));
        Assert.Contains($"Startup refused in \"{environment}\"", critical);
        Assert.Contains(flag, critical);
    }

    [Fact]
    public async Task Development_host_starts_with_test_flags_on()
    {
        _factory.CreateClient();
        var logs = new LogCapture();
        using var host = HostIn(Environments.Development, logs,
            ("Razorpay:UseFakeGateway", "true"), ("Otp:ExposeCode", "true"), ("Otp:UseFixedCode", "true"));

        var response = await host.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(logs.Lines, line => line.StartsWith("Critical", StringComparison.Ordinal));
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

    private WebApplicationFactory<Program> HostIn(string environment, LogCapture logs, params (string Key, string Value)[] overrides) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // UseSetting, unlike ConfigureAppConfiguration, is already visible to the checks Program runs before Build.
            foreach (var (key, value) in SafeProduction)
                builder.UseSetting(key, value);
            if (environment == Environments.Staging)
                foreach (var (key, value) in StagingKeys)
                    builder.UseSetting(key, value);
            builder.UseSetting("Cors:AllowedOrigins:0", "https://app.example.test");
            builder.UseSetting("Jwt:Key", "testing-key-at-least-32-characters-long");
            foreach (var (key, value) in overrides)
                builder.UseSetting(key, value);
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

    private static IConfiguration Config(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>(SafeProduction);
        foreach (var (key, value) in overrides)
            values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed record ErrorBody(string Error, string? TraceId);

    private sealed class LogCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Lines);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue($"{logLevel} {formatter(state, exception)}");
        }
    }
}

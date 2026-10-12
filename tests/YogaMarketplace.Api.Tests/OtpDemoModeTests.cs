using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Api.Security;

namespace YogaMarketplace.Api.Tests;

public class OtpDemoModeTests : IClassFixture<YogaApiFactory>
{
    private const string DemoCode = "246810";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public OtpDemoModeTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Only_listed_phones_get_the_demo_code()
    {
        var demo = OtpDemoMode.From(new OtpOptions { DemoPhones = "+919811100001, +919811100002", DemoCode = DemoCode });

        Assert.True(demo.IsOn);
        Assert.Equal(DemoCode, demo.CodeFor("+919811100001"));
        Assert.Equal(DemoCode, demo.CodeFor("+919811100002"));
        Assert.Null(demo.CodeFor("+919811100003"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void An_empty_list_means_the_demo_code_never_works(string? phones)
    {
        var demo = OtpDemoMode.From(new OtpOptions { DemoPhones = phones!, DemoCode = DemoCode });

        Assert.False(demo.IsOn);
        Assert.Null(demo.CodeFor("+919811100001"));
        Assert.Null(demo.StartupWarning());
    }

    [Fact]
    public void Startup_warning_masks_the_phones()
    {
        var warning = OtpDemoMode.From(new OtpOptions { DemoPhones = "+919811100001", DemoCode = DemoCode }).StartupWarning();

        Assert.NotNull(warning);
        Assert.Contains("*********0001", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("9811100001", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(DemoCode, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_listed_phone_signs_in_with_the_demo_code()
    {
        var phone = NewPhone();
        await using var app = WithDemo(phone);
        var client = app.CreateClient();

        var otp = await RequestAsync(client, phone);
        Assert.Null(otp.DevCode);

        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = DemoCode });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task Any_other_phone_still_gets_a_normal_code()
    {
        var other = NewPhone();
        await using var app = WithDemo(NewPhone());
        var client = app.CreateClient();
        await RequestAsync(client, other);

        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone = other, code = DemoCode });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal("That code is incorrect.", (await verify.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error);
    }

    [Fact]
    public async Task Without_a_phone_list_the_demo_code_is_rejected()
    {
        var phone = NewPhone();
        await using var app = WithDemo(phones: "");
        var client = app.CreateClient();
        await RequestAsync(client, phone);

        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = DemoCode });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    [Fact]
    public async Task Demo_phones_are_locked_out_after_too_many_wrong_codes()
    {
        var phone = NewPhone();
        await using var app = WithDemo(phone);
        var client = app.CreateClient();
        await RequestAsync(client, phone);

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "000000" });
        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = DemoCode });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal("Too many attempts. Request a new code.", (await verify.Content.ReadFromJsonAsync<ErrorBody>(Json))!.Error);
    }

    [Fact]
    public async Task Demo_phones_are_rate_limited()
    {
        var phone = NewPhone();
        await using var app = WithDemo(phone, services => services.Configure<RateLimitOptions>(options =>
        {
            options.Enabled = true;
            options.OtpPerMinute = 2;
        }));
        var client = app.CreateClient();
        await RequestAsync(client, phone);
        await client.PostAsJsonAsync("/api/auth/otp/resend", new { phone });

        var blocked = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = DemoCode });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    private WebApplicationFactory<Program> WithDemo(string phones, Action<IServiceCollection>? extra = null) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Configure<OtpOptions>(options =>
            {
                options.ExposeCode = false;
                options.DemoPhones = phones;
                options.DemoCode = DemoCode;
            });
            extra?.Invoke(services);
        }));

    private static async Task<OtpBody> RequestAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/auth/otp/request",
            new { phone, name = "Demo Visitor", gender = "Female", isNewUser = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OtpBody>(Json))!;
    }

    private static string NewPhone() => "+9197" + Random.Shared.Next(10000000, 99999999);

    private sealed record OtpBody(string? DevCode);

    private sealed record ErrorBody(string Error);
}

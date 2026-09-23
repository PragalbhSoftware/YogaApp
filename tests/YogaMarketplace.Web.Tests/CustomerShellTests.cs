extern alias WebApp;

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using WebApp::YogaMarketplace.Web.Copy;
using WebApp::YogaMarketplace.Web.Services;
using YogaMarketplace.Api.Tests;

namespace YogaMarketplace.Web.Tests;

public class CustomerShellTests : IClassFixture<YogaApiFactory>
{
    private readonly YogaApiFactory _api;

    public CustomerShellTests(YogaApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Home_offers_sign_in_and_browse_requires_an_account()
    {
        await using var web = new WebApplicationFactory<WebApp::Program>();
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var home = await client.GetStringAsync("/");
        Assert.Contains(UiCopy.SignInCta, home);
        Assert.Contains(UiCopy.CategoryName, home);
        Assert.Contains(UiCopy.CityName, home);

        var browse = await client.GetAsync("/instructors");
        Assert.Equal(HttpStatusCode.Redirect, browse.StatusCode);
        Assert.Contains("/account/sign-in", browse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var areas = await client.GetAsync("/areas");
        Assert.Equal(HttpStatusCode.Redirect, areas.StatusCode);
    }

    [Fact]
    public async Task New_customer_must_send_name_and_gender_before_the_api_is_called()
    {
        var api = new CountingApi();
        await using var web = new WebApplicationFactory<WebApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => ReplaceApi(services, api));
        });
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var html = await client.GetStringAsync("/account/sign-in");
        Assert.Contains("123456", html);
        Assert.Contains("devCode", html);

        var response = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "new",
            ["Phone"] = "9876501111"
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(UiCopy.NameRequired, body);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task Sign_in_shows_an_error_when_the_api_is_down()
    {
        await using var web = new WebApplicationFactory<WebApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => ReplaceApi(services, new DownApi()));
        });
        var client = web.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var html = await client.GetStringAsync("/account/sign-in");

        var response = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "existing",
            ["Phone"] = "9876543210"
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Start the API and try again.", body);
        Assert.Contains(UiCopy.SendCode, body);
    }

    [Fact]
    public async Task Customer_can_verify_otp_choose_bandra_and_open_ananya()
    {
        await using var web = CreateWeb(_api);
        var client = web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var phone = "98" + Random.Shared.Next(10000000, 99999999);
        var html = await client.GetStringAsync("/account/sign-in");
        var requested = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "new",
            ["Name"] = "Rahul Sharma",
            ["Gender"] = "Male",
            ["Phone"] = phone
        }));
        html = await requested.Content.ReadAsStringAsync();
        Assert.True(requested.IsSuccessStatusCode, html);
        var code = Regex.Match(html, "data-dev-code=\"([^\"]+)\"");
        Assert.True(code.Success, html);

        var verified = await client.PostAsync("/account/sign-in?handler=Verify", Form(html, new Dictionary<string, string>
        {
            ["Phone"] = phone,
            ["Code"] = code.Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.Equal("/areas", verified.Headers.Location?.OriginalString);

        var areas = await client.GetAsync("/areas");
        html = await areas.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, areas.StatusCode);
        Assert.Contains("Bandra", html);
        Assert.Contains("Andheri", html);
        Assert.Contains("Mumbai", html);

        var picked = await client.PostAsync("/areas", Form(html, new Dictionary<string, string>
        {
            ["area"] = "Bandra"
        }));
        Assert.Equal(HttpStatusCode.Redirect, picked.StatusCode);
        Assert.Equal("/instructors", picked.Headers.Location?.OriginalString);

        var list = await client.GetAsync("/instructors");
        html = await list.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("Ananya Desai", html);
        Assert.Contains("Verified", html);
        Assert.Contains("899", html);
        Assert.Contains("Home", html);
        Assert.DoesNotContain("meet.google.com", html, StringComparison.OrdinalIgnoreCase);

        var id = Regex.Match(html, "/instructors/([0-9a-fA-F-]{36})").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(id));

        var home = await client.GetStringAsync($"/instructors/{id}?mode=Home");
        Assert.Contains("Ananya Desai", home);
        Assert.Contains("Lotus Studio", home);
        Assert.Contains("07:00", home);
        Assert.Contains(UiCopy.SlotsLead, home);
        Assert.DoesNotContain("meet.google.com", home, StringComparison.OrdinalIgnoreCase);

        var online = await client.GetStringAsync($"/instructors/{id}?mode=Online");
        Assert.Contains("18:00", online);
        Assert.DoesNotContain("07:00", online);

        var andheri = await client.GetStringAsync("/instructors?area=Andheri&mode=Home");
        Assert.DoesNotContain("Ananya Desai", andheri);
        Assert.Contains("No verified instructors in Andheri", andheri);

        var signedOut = await client.PostAsync("/account/sign-out", Form(home, new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        var blocked = await client.GetAsync("/instructors");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Contains("/account/sign-in", blocked.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Existing_phone_signs_in_without_name()
    {
        await using var web = CreateWeb(_api);
        var client = web.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var html = await client.GetStringAsync("/account/sign-in");
        var requested = await client.PostAsync("/account/sign-in?handler=Request", Form(html, new Dictionary<string, string>
        {
            ["AccountKind"] = "existing",
            ["Phone"] = "9876543210"
        }));
        html = await requested.Content.ReadAsStringAsync();
        Assert.True(requested.IsSuccessStatusCode, html);
        Assert.DoesNotContain("Name is required", html);
        var code = Regex.Match(html, "data-dev-code=\"([^\"]+)\"");
        Assert.True(code.Success, html);

        var verified = await client.PostAsync("/account/sign-in?handler=Verify", Form(html, new Dictionary<string, string>
        {
            ["Phone"] = "9876543210",
            ["Code"] = code.Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);

        var areas = await client.GetAsync(verified.Headers.Location);
        var body = await areas.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, areas.StatusCode);
        Assert.Contains("Ananya Desai", body);
    }

    private WebApplicationFactory<WebApp::Program> CreateWeb(YogaApiFactory api)
    {
        _ = api.Server;
        return new WebApplicationFactory<WebApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = api.Server.BaseAddress.ToString(),
                    ["Api:CategorySlug"] = "yoga",
                    ["Api:TimeoutSeconds"] = "30"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HttpClientFactoryOptions>(MarketplaceApiClient.HttpClientName, options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = api.Server.CreateHandler();
                    });
                });
            });
        });
    }

    private static void ReplaceApi(IServiceCollection services, IMarketplaceApi api)
    {
        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(IMarketplaceApi)).ToList();
        foreach (var descriptor in existing)
            services.Remove(descriptor);
        services.AddSingleton(api);
    }

    private static FormUrlEncodedContent Form(string html, IDictionary<string, string> fields)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, html);
        var pairs = fields.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)).ToList();
        pairs.Add(new KeyValuePair<string, string>("__RequestVerificationToken", token.Groups[1].Value));
        return new FormUrlEncodedContent(pairs);
    }

    private class DownApi : IMarketplaceApi
    {
        public virtual Task<ApiResult<OtpResponseDto>> RequestOtpAsync(OtpRequestDto request, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<OtpResponseDto>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<OtpResponseDto>> ResendOtpAsync(string phone, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<OtpResponseDto>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<VerifyResponseDto>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<VerifyResponseDto>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<List<AreaDto>>> GetAreasAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<List<AreaDto>>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<List<ProviderSummaryDto>>> BrowseAsync(string? area, string? mode, string? category, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<List<ProviderSummaryDto>>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<ProviderDetailDto>> GetProviderAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<ProviderDetailDto>.Down(UiCopy.ApiUnreachable));

        public Task<ApiResult<SlotListDto>> GetSlotsAsync(Guid id, string mode, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult<SlotListDto>.Down(UiCopy.ApiUnreachable));
    }

    private sealed class CountingApi : DownApi
    {
        public int Calls { get; private set; }

        public override Task<ApiResult<OtpResponseDto>> RequestOtpAsync(OtpRequestDto request, CancellationToken cancellationToken)
        {
            Calls++;
            return base.RequestOtpAsync(request, cancellationToken);
        }
    }
}

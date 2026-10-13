using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class DemoDataLoaderTests : IClassFixture<YogaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly YogaApiFactory _factory;

    public DemoDataLoaderTests(YogaApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Loading_twice_gives_the_same_data_with_no_duplicates()
    {
        var customer = await SignInAsync("+917000099001", isNewUser: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsync("/api/admin/demo-data/load", null)).StatusCode);

        var admin = await SignInAsync(SeedIds.AdminPhone, isNewUser: false);
        var first = await LoadAsync(admin);
        var afterFirst = await CountsAsync();
        var second = await LoadAsync(admin);
        var afterSecond = await CountsAsync();

        Assert.True(first.Created > 0);
        Assert.Equal(0, first.BookingsSkipped);
        Assert.Equal(new DemoLoadResult(0, 0, 0), second);
        Assert.Equal(afterFirst, afterSecond);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var demoPayments = await db.Payments.AsNoTracking()
            .Where(p => p.GatewayPaymentId!.StartsWith(DemoDataLoader.TestPaymentPrefix))
            .Include(p => p.Booking)
            .ToListAsync();
        Assert.Equal(3, demoPayments.Count);
        Assert.All(demoPayments, p => Assert.Equal(PaymentStatus.Paid, p.Status));
        Assert.Equal(
            new[] { BookingStatus.Completed, BookingStatus.PendingAccept, BookingStatus.Upcoming },
            demoPayments.Select(p => p.Booking!.Status).OrderBy(s => s.ToString()).ToArray());
        var settings = await db.Settings.AsNoTracking().SingleAsync();
        Assert.All(demoPayments, p => Assert.Equal(settings.CommissionPercent, p.Booking!.CommissionPercent));
    }

    [Fact]
    public async Task Loader_refuses_to_run_in_Production_and_writes_nothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var before = await CountsAsync();

        var loader = new DemoDataLoader(db, config, new StubEnvironment(Environments.Production));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(testPayments: true));

        Assert.Equal(before, await CountsAsync());
    }

    [Fact]
    public async Task Loader_runs_on_Staging()
    {
        using var factory = new YogaApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var loader = new DemoDataLoader(db, config, new StubEnvironment(Environments.Staging));
        var result = await loader.LoadAsync(testPayments: true);

        Assert.True(result.Created > 0);
    }

    private async Task<string> CountsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
        return string.Join(',',
            await db.Areas.CountAsync(),
            await db.Users.CountAsync(),
            await db.Providers.CountAsync(),
            await db.Services.CountAsync(),
            await db.AvailabilitySlots.CountAsync(),
            await db.CreditPacks.CountAsync(),
            await db.Bookings.CountAsync(),
            await db.Payments.CountAsync(),
            await db.PayoutsPending.CountAsync());
    }

    private static async Task<DemoLoadResult> LoadAsync(HttpClient admin)
    {
        var response = await admin.PostAsync("/api/admin/demo-data/load", null);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<DemoLoadResult>(payload, Json)!;
    }

    private async Task<HttpClient> SignInAsync(string phone, bool isNewUser)
    {
        var client = _factory.CreateClient();
        var otp = await PostAsync<OtpBody>(client, "/api/auth/otp/request", new { phone, name = "Demo Gate", gender = "Other", isNewUser });
        var auth = await PostAsync<AuthBody>(client, "/api/auth/otp/verify", new { phone, code = otp.DevCode });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<T>(payload, Json)!;
    }

    private sealed record OtpBody(string? DevCode);
    private sealed record AuthBody(string Token);

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "YogaMarketplace.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

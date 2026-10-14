using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public class YogaApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Tests only: far above the admin maximums, which real settings can never reach.</summary>
    public const int UnlimitedOtp = 10_000;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"yoga-marketplace-{Guid.NewGuid():N}.db");
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"yoga-marketplace-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=localhost,1433;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True",
                ["Jwt:Issuer"] = "YogaMarketplace",
                ["Jwt:Audience"] = "YogaMarketplace",
                ["Jwt:Key"] = "testing-key-at-least-32-characters-long",
                ["Jwt:ExpiresMinutes"] = "60",
                ["Otp:ExposeCode"] = "true",
                ["Otp:UseFixedCode"] = "false",
                ["Otp:Pepper"] = "test-otp-pepper",
                ["Otp:ExpiryMinutes"] = "5",
                ["Otp:MaxAttempts"] = "5",
                ["Database:AutoMigrate"] = "false",
                ["Seed:DemoData"] = "true",
                ["Razorpay:KeyId"] = "rzp_test_placeholder",
                ["Razorpay:KeySecret"] = "dev-only-not-a-live-key-secret",
                ["Razorpay:WebhookSecret"] = "dev-only-not-a-live-webhook-secret",
                ["Razorpay:UseFakeGateway"] = "true",
                ["Storage:RootPath"] = _storagePath
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<YogaDbContext>)
                || d.ServiceType == typeof(YogaDbContext)).ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<YogaDbContext>((provider, options) =>
            {
                UseDatabase(options);
                options.AddInterceptors(provider.GetServices<IInterceptor>());
            });
            services.AddHostedService(provider => new TestDatabaseStartup(provider, this));
        });
    }

    protected virtual void UseDatabase(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={_dbPath}");

    protected virtual Task CreateDatabaseAsync(YogaDbContext db, CancellationToken cancellationToken) =>
        db.Database.EnsureCreatedAsync(cancellationToken);

    protected virtual void DeleteDatabase()
    {
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        DeleteDatabase();
        try
        {
            if (Directory.Exists(_storagePath))
                Directory.Delete(_storagePath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed class TestDatabaseStartup : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly YogaApiFactory _factory;

        public TestDatabaseStartup(IServiceProvider services, YogaApiFactory factory)
        {
            _services = services;
            _factory = factory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            await _factory.CreateDatabaseAsync(db, cancellationToken);
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(cancellationToken);
            // Shared factories sign the same seeded phones in many times. OtpPhoneLimitTests sets real limits itself.
            await db.Settings.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.OtpMaxSendsPerPhone, UnlimitedOtp)
                .SetProperty(x => x.OtpMaxWrongCodesPerPhone, UnlimitedOtp), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

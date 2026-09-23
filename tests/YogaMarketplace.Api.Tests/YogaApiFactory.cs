using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public sealed class YogaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"yoga-marketplace-{Guid.NewGuid():N}.db");

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
                ["Seed:DemoData"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<YogaDbContext>)
                || d.ServiceType == typeof(YogaDbContext)).ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<YogaDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
            services.AddHostedService<TestDatabaseStartup>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
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

        public TestDatabaseStartup(IServiceProvider services)
        {
            _services = services;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YogaDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

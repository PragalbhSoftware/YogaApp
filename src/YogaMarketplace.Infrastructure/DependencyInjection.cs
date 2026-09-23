using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<YogaDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DbSeeder>();
        services.AddHealthChecks()
            .AddDbContextCheck<YogaDbContext>("database", tags: new[] { "ready" });

        return services;
    }
}

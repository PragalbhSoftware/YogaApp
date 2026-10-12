using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// The API on a throwaway SQL Server database built from the real migrations, so filtered unique indexes and other
/// SQL Server behaviour are exercised. Only use from <see cref="SqlServerFactAttribute"/> tests.
/// </summary>
public sealed class SqlServerYogaApiFactory : YogaApiFactory
{
    private readonly string _connection = SqlServerFactAttribute.ConnectionFor($"YogaApiTest_{Guid.NewGuid():N}");

    protected override void UseDatabase(DbContextOptionsBuilder options) => options.UseSqlServer(_connection);

    protected override Task CreateDatabaseAsync(YogaDbContext db, CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);

    protected override void DeleteDatabase()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)))
            return;
        using var db = new YogaDbContext(new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(_connection).Options);
        db.Database.EnsureDeleted();
    }
}

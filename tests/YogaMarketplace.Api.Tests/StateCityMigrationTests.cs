using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public sealed class StateCityMigrationTests : IAsyncLifetime
{
    private const string BeforeStates = "20261014114633_AddBookingRequestEmails";

    private readonly string _connection = SqlServerFactAttribute.ConnectionFor($"YogaMigTest_{Guid.NewGuid():N}");

    [SqlServerFact]
    public async Task Up_adds_empty_state_and_city_tables_and_down_removes_them()
    {
        await MigrateAsync(null);
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM States"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM Cities"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'City'"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'CityId'"));

        await MigrateAsync(BeforeStates);
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'States'"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'Cities'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'City'"));

        await MigrateAsync(null);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'States'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'Cities'"));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)))
            return;
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task<int> ScalarAsync(FormattableString sql)
    {
        await using var db = NewContext();
        return await db.Database.SqlQuery<int>(sql).SingleAsync();
    }

    private async Task MigrateAsync(string? target)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private YogaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(_connection).Options);
}

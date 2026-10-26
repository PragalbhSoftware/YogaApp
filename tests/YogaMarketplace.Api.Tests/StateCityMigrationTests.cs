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
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM Areas"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'City'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'CityId'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'Pincode'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE object_id = OBJECT_ID('Areas') AND name = 'IX_Areas_CityId_Name' AND is_unique = 1"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.foreign_keys WHERE name = 'FK_Areas_Cities_CityId' AND delete_referential_action = 0"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE object_id = OBJECT_ID('States') AND name = 'IX_States_Code' AND is_unique = 1"));

        await MigrateAsync(BeforeStates);
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'States'"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'Cities'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'City'"));

        await MigrateAsync(null);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'States'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'Cities'"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'CityId'"));
    }

    [SqlServerFact]
    public async Task Up_fails_when_areas_still_have_rows()
    {
        var connection = SqlServerFactAttribute.ConnectionFor($"YogaMigArea_{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(connection).Options;
        await using var db = new YogaDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261015124321_UniqueStateCode");
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Areas (Id, City, Name, IsActive) VALUES ({0}, {1}, {2}, {3})",
                Guid.NewGuid(), "Harbor", "Bandra", true);

            var failed = false;
            try
            {
                await db.GetService<IMigrator>().MigrateAsync(null);
            }
            catch (Exception)
            {
                failed = true;
            }

            Assert.True(failed);
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'City'").SingleAsync());
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Areas') AND name = 'CityId'").SingleAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
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

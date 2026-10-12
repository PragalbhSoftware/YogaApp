using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public sealed class DropOldTrialIndexMigrationTests : IAsyncLifetime
{
    private const string BeforeDrop = "20261012071209_AddCreditPacks";
    private const string OldIndex = "IX_Bookings_CustomerId_ProviderId_IsTrial";
    private const string UniqueIndex = "IX_Bookings_OneConsumedTrial";

    private readonly string _connection = SqlServerFactAttribute.ConnectionFor($"YogaMigTest_{Guid.NewGuid():N}");

    [SqlServerFact]
    public async Task Up_drops_the_old_trial_index_and_down_brings_it_back()
    {
        await MigrateAsync(null);
        Assert.False(await IndexExistsAsync(OldIndex));
        Assert.True(await IndexExistsAsync(UniqueIndex));

        await MigrateAsync(BeforeDrop);
        Assert.True(await IndexExistsAsync(OldIndex));
        Assert.True(await IndexExistsAsync(UniqueIndex));

        await MigrateAsync(null);
        Assert.False(await IndexExistsAsync(OldIndex));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)))
            return;
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task<bool> IndexExistsAsync(string name)
    {
        await using var db = NewContext();
        return await db.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = {name} AND object_id = OBJECT_ID('Bookings')").SingleAsync() == 1;
    }

    private async Task MigrateAsync(string? target)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private YogaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(_connection).Options);
}

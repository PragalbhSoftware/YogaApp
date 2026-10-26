using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public sealed class CreditPacksMigrationTests : IAsyncLifetime
{
    private const string BeforeCreditPacks = "20261012063600_AddTrialConsumed";

    private readonly string _connection = SqlServerFactAttribute.ConnectionFor($"YogaMigTest_{Guid.NewGuid():N}");

    [SqlServerFact]
    public async Task Down_removes_pack_checkouts_keeps_booking_checkouts_and_up_runs_again()
    {
        await MigrateAsync(null);
        await using (var db = NewContext())
        {
            await new DbSeeder(db, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoData"] = "true" })
                .Build(), TimeProvider.System).SeedAsync();
            await TestWorld.SeedAsync(db, TimeProvider.System, CancellationToken.None);
            var customer = new User
            {
                Id = Guid.NewGuid(),
                Phone = "+9193" + Random.Shared.Next(10000000, 99999999),
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var pack = new CreditPack
            {
                Id = Guid.NewGuid(),
                Name = "8 sessions",
                Sessions = 8,
                Price = 4800m,
                ValidityDays = 30,
                PriceCap = 800m,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var checkout = new CheckoutIntent
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Kind = CheckoutKind.Pack,
                PackId = pack.Id,
                Amount = pack.Price,
                GatewayOrderId = "order_mig_pack",
                GatewayPaymentId = "pay_mig_pack",
                Status = CheckoutStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var slot = await db.AvailabilitySlots.FirstAsync(s => s.ProviderId == SeedIds.AnanyaProviderId);
            var bookingCheckout = new CheckoutIntent
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                ProviderId = slot.ProviderId,
                ServiceId = SeedIds.AnanyaServiceId,
                SlotId = slot.Id,
                Mode = slot.Mode,
                Amount = 599m,
                GatewayOrderId = "order_mig_booking",
                Status = CheckoutStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.AddRange(customer, pack, checkout, bookingCheckout);
            await db.SaveChangesAsync();

            var settings = await db.Settings.SingleAsync();
            Assert.False(settings.CreditConvenienceFee);
            Assert.True(settings.CreditBookingStandsAfterExpiry);
            Assert.True(settings.PackRefundsEnabled);
            Assert.Equal(799m, settings.PackReferenceSessionPrice);
        }

        await MigrateAsync(BeforeCreditPacks);
        await using (var db = NewContext())
        {
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM CheckoutIntents WHERE GatewayOrderId = 'order_mig_pack'").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM CheckoutIntents WHERE GatewayOrderId = 'order_mig_booking'").SingleAsync());
            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM Payments;
                DELETE FROM Bookings;
                DELETE FROM CheckoutIntents;
                DELETE FROM AvailabilitySlots;
                DELETE FROM Services;
                DELETE FROM Providers;
                DELETE FROM Areas;
                """);
        }

        await MigrateAsync(null);
        await using (var db = NewContext())
        {
            Assert.Contains("20261012071209_AddCreditPacks", await db.Database.GetAppliedMigrationsAsync());
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

    private async Task MigrateAsync(string? target)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private YogaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(_connection).Options);
}

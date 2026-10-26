using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

public sealed class TrialConsumedMigrationTests : IAsyncLifetime
{
    private const string BeforeTrialConsumed = "20261012060558_AddContentPages";

    private readonly string _connection = SqlServerFactAttribute.ConnectionFor($"YogaMigTest_{Guid.NewGuid():N}");
    private readonly DateOnly _day = MarketClock.Today(DateTimeOffset.UtcNow).AddDays(2);
    private int _slotHour = 5;

    [SqlServerFact]
    public async Task Backfill_follows_the_trial_rule_and_skips_a_duplicate_refunded_at_capture()
    {
        var expected = await SeedAsync(async db =>
        {
            var kept = await CustomerAsync(db);
            var keptTrial = Book(db, kept, BookingStatus.Upcoming);
            var refundedDuplicate = Book(db, kept, BookingStatus.PendingAccept);
            RefundedAtCapture(db, refundedDuplicate);

            var givenBack = await CustomerAsync(db);
            var declined = Book(db, givenBack, BookingStatus.Declined);
            var adminCancelled = Book(db, givenBack, BookingStatus.Cancelled, CancelledBy.Admin);

            var customerCancelled = await CustomerAsync(db);
            var cancelledTrial = Book(db, customerCancelled, BookingStatus.Cancelled, CancelledBy.Customer);
            var regular = Book(db, customerCancelled, BookingStatus.Completed, trial: false);

            return new Dictionary<Guid, bool>
            {
                [keptTrial] = true,
                [refundedDuplicate] = false,
                [declined] = false,
                [adminCancelled] = false,
                [cancelledTrial] = true,
                [regular] = false
            };
        });

        await MigrateAsync("20261015124321_UniqueStateCode");

        await using (var db = NewContext())
        {
            var stored = await db.Bookings.Where(b => expected.Keys.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.TrialConsumed);
            Assert.Equal(expected, stored);
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
    }

    [SqlServerFact]
    public async Task Two_active_trials_for_the_same_pair_stop_the_migration_with_a_clear_message()
    {
        await SeedAsync(async db =>
        {
            var customer = await CustomerAsync(db);
            Book(db, customer, BookingStatus.Upcoming);
            Book(db, customer, BookingStatus.Completed);
            return 0;
        });

        var error = await Assert.ThrowsAsync<SqlException>(() => MigrateAsync(null));
        Assert.Contains("more than one active trial", error.Message);

        await using var db = NewContext();
        Assert.DoesNotContain("20261012063600_AddTrialConsumed", await db.Database.GetAppliedMigrationsAsync());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)))
            return;
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// Builds the current schema to insert through EF, then steps back to the migration before TrialConsumed so the
    /// rows look like existing production data. The flag is written as false to get past the new index.
    /// </summary>
    private async Task<T> SeedAsync<T>(Func<YogaDbContext, Task<T>> seed)
    {
        await MigrateAsync(null);
        await using (var db = NewContext())
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoData"] = "true" })
                .Build();
            await new DbSeeder(db, config, TimeProvider.System).SeedAsync();
            await TestWorld.SeedAsync(db, TimeProvider.System, CancellationToken.None);
            var result = await seed(db);
            await db.SaveChangesAsync();
            await MigrateAsync(BeforeTrialConsumed);
            return result;
        }
    }

    private async Task MigrateAsync(string? target)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private YogaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YogaDbContext>().UseSqlServer(_connection).Options);

    private static async Task<Guid> CustomerAsync(YogaDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Phone = "+9193" + Random.Shared.Next(10000000, 99999999),
            Name = "Test Customer",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private Guid Book(YogaDbContext db, Guid customerId, BookingStatus status, CancelledBy? cancelledBy = null, bool trial = true)
    {
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            ProviderId = SeedIds.AnanyaProviderId,
            Mode = SessionMode.Online,
            Date = _day,
            StartTime = new TimeOnly(_slotHour, 0),
            EndTime = new TimeOnly(_slotHour, 30)
        };
        _slotHour++;
        db.AvailabilitySlots.Add(slot);

        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProviderId = SeedIds.AnanyaProviderId,
            ServiceId = SeedIds.AnanyaServiceId,
            SlotId = slot.Id,
            Mode = SessionMode.Online,
            Status = status,
            CancelledBy = cancelledBy,
            Amount = trial ? 99m : 599m,
            IsTrial = trial,
            TrialConsumed = false,
            MeetLinkSnapshot = "https://meet.google.com/abc-defg-hij",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Bookings.Add(booking);
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = booking.Amount,
            Status = PaymentStatus.Paid,
            Gateway = PaymentGateways.Razorpay,
            GatewayOrderId = $"order_mig_{booking.Id:N}",
            GatewayPaymentId = $"pay_mig_{booking.Id:N}",
            CreatedAt = now
        });
        return booking.Id;
    }

    private static void RefundedAtCapture(YogaDbContext db, Guid bookingId)
    {
        var booking = db.Bookings.Local.Single(b => b.Id == bookingId);
        db.CheckoutIntents.Add(new CheckoutIntent
        {
            Id = Guid.NewGuid(),
            CustomerId = booking.CustomerId,
            ProviderId = booking.ProviderId,
            ServiceId = booking.ServiceId,
            SlotId = booking.SlotId,
            Mode = booking.Mode,
            Amount = booking.Amount,
            IsTrial = true,
            GatewayOrderId = $"order_mig_{booking.Id:N}",
            Status = CheckoutStatus.Refunded,
            CreatedAt = booking.CreatedAt,
            CompletedAt = booking.CreatedAt
        });
    }
}

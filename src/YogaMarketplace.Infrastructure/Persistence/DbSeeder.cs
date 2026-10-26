using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

public class DbSeeder
{
    private readonly YogaDbContext _db;
    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    public DbSeeder(YogaDbContext db, IConfiguration config, TimeProvider time)
    {
        _db = db;
        _config = config;
        _time = time;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _db.Categories.AnyAsync(cancellationToken))
        {
            _db.Categories.Add(new Category
            {
                Id = SeedIds.YogaCategoryId,
                Name = "Yoga",
                Slug = "yoga",
                IsActive = true
            });
        }

        if (!await _db.Settings.AnyAsync(cancellationToken))
        {
            _db.Settings.Add(new PlatformSettings
            {
                Id = SeedIds.PolicyId,
                Currency = "INR",
                CommissionPercent = 15m,
                ConvenienceFee = 0m,
                CancelFreeWindowHours = 12,
                RescheduleFreeWindowHours = 12,
                LateCancelFeeType = LateCancelFeeType.Percent,
                LateCancelFeeValue = 50m,
                PayoutCycle = PayoutCycle.Weekly,
                PolicyNote = "Platform fee, cancel window, and reschedule window are launch defaults. Confirm with ops before taking live payments.",
                TimeZoneId = MarketClock.DefaultTimeZoneId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (bool.TryParse(_config["Seed:DemoData"], out var demoData) && demoData)
            await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await _db.Users.AnyAsync(u => u.Id == SeedIds.AdminUserId, cancellationToken))
            return;

        _db.Users.Add(new User
        {
            Id = SeedIds.AdminUserId,
            Phone = SeedIds.AdminPhone,
            Name = "Marketplace Admin",
            Email = "admin@example.com",
            Role = UserRole.Admin,
            CreatedAt = _time.GetUtcNow()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

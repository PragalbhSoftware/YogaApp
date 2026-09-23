using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

public class DbSeeder
{
    private readonly YogaDbContext _db;
    private readonly IConfiguration _config;

    public DbSeeder(YogaDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _db.Areas.AnyAsync(cancellationToken))
        {
            _db.Areas.AddRange(
                Area(SeedIds.AreaBandra, "Bandra"),
                Area(SeedIds.AreaAndheri, "Andheri"),
                Area(SeedIds.AreaPowai, "Powai"),
                Area(SeedIds.AreaJuhu, "Juhu"),
                Area(SeedIds.AreaLowerParel, "Lower Parel"),
                Area(SeedIds.AreaDadar, "Dadar"),
                Area(SeedIds.AreaWorli, "Worli"));
        }

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

        if (!await _db.Policies.AnyAsync(cancellationToken))
        {
            _db.Policies.Add(new MarketplacePolicy
            {
                Id = SeedIds.PolicyId,
                Currency = "INR",
                PlatformFeePercent = 15m,
                CancelFreeWindowHours = 12,
                RescheduleFreeWindowHours = 12,
                LateCancelFeePercent = 50m,
                PolicyNote = "Platform fee, cancel window, and reschedule window are TBD defaults for the Mumbai launch. Confirm with ops before taking live payments."
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (bool.TryParse(_config["Seed:DemoData"], out var demoData) && demoData)
            await SeedDemoAsync(cancellationToken);
    }

    private async Task SeedDemoAsync(CancellationToken cancellationToken)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == SeedIds.AdminUserId, cancellationToken))
        {
            _db.Users.Add(new User
            {
                Id = SeedIds.AdminUserId,
                Phone = SeedIds.AdminPhone,
                Name = "Marketplace Admin",
                Email = "admin@yogamumbai.in",
                Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (await _db.Providers.AnyAsync(p => p.Id == SeedIds.AnanyaProviderId, cancellationToken))
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _db.Users.Add(new User
        {
            Id = SeedIds.AnanyaUserId,
            Phone = SeedIds.AnanyaPhone,
            Name = "Ananya Desai",
            Gender = Gender.Female,
            Email = "ananya@example.com",
            Role = UserRole.Provider,
            CreatedAt = now
        });

        var provider = new Provider
        {
            Id = SeedIds.AnanyaProviderId,
            UserId = SeedIds.AnanyaUserId,
            Status = ProviderStatus.Verified,
            DisplayName = "Ananya Desai",
            Bio = "Hatha and restorative yoga. Home sessions in Bandra.",
            Age = 32,
            AreaId = SeedIds.AreaBandra,
            OffersHome = true,
            OffersStudio = true,
            OffersOnline = true,
            HomeRate = 899m,
            StudioRate = 749m,
            OnlineRate = 599m,
            StudioAddress = "Lotus Studio, Bandra West, Mumbai",
            GoogleMeetLink = "https://meet.google.com/abc-defg-hij",
            CreatedAt = now,
            ReviewedAt = now
        };
        _db.Providers.Add(provider);
        _db.Services.Add(new Service
        {
            Id = SeedIds.AnanyaServiceId,
            ProviderId = provider.Id,
            CategoryId = SeedIds.YogaCategoryId,
            Title = "Yoga session",
            IsActive = true
        });

        var today = MumbaiClock.Today();
        for (var day = 0; day < 7; day++)
        {
            var date = today.AddDays(day);
            AddSlot(provider.Id, SessionMode.Home, date, new TimeOnly(7, 0), new TimeOnly(8, 0));
            AddSlot(provider.Id, SessionMode.Home, date, new TimeOnly(8, 0), new TimeOnly(9, 0));
            AddSlot(provider.Id, SessionMode.Studio, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            AddSlot(provider.Id, SessionMode.Online, date, new TimeOnly(18, 0), new TimeOnly(19, 0));
            AddSlot(provider.Id, SessionMode.Online, date, new TimeOnly(19, 0), new TimeOnly(20, 0));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void AddSlot(Guid providerId, SessionMode mode, DateOnly date, TimeOnly start, TimeOnly end)
    {
        _db.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Mode = mode,
            Date = date,
            StartTime = start,
            EndTime = end,
            IsBlocked = false
        });
    }

    private static Area Area(Guid id, string name) => new()
    {
        Id = id,
        City = "Mumbai",
        Name = name,
        IsActive = true
    };
}

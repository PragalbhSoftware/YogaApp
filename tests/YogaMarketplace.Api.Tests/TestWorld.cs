using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Tests;

/// <summary>The instructor and neighbourhoods the API tests book against. The product seeder does not create them.</summary>
internal static class TestWorld
{
    public const string CityName = "Harbor";
    public static readonly Guid StateId = Guid.Parse("c0000001-0000-4000-8000-000000000001");
    public static readonly Guid CityId = Guid.Parse("c0000001-0000-4000-8000-000000000002");

    public static async Task SeedAsync(YogaDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await db.Areas.AnyAsync(cancellationToken))
            return;

        db.States.Add(new State { Id = StateId, Name = "Harborland", Code = "HL", IsActive = true });
        db.Cities.Add(new City
        {
            Id = CityId,
            Name = CityName,
            StateId = StateId,
            IsActive = true,
            Slug = "hl-harbor"
        });
        db.Areas.AddRange(
            Area(SeedIds.AreaBandra, "Bandra"),
            Area(SeedIds.AreaAndheri, "Andheri"),
            Area(SeedIds.AreaPowai, "Powai"),
            Area(SeedIds.AreaJuhu, "Juhu"),
            Area(SeedIds.AreaLowerParel, "Lower Parel"),
            Area(SeedIds.AreaDadar, "Dadar"),
            Area(SeedIds.AreaWorli, "Worli"));

        var now = time.GetUtcNow();
        db.Users.Add(new User
        {
            Id = SeedIds.AnanyaUserId,
            Phone = SeedIds.AnanyaPhone,
            Name = "Ananya Desai",
            Gender = Gender.Female,
            Email = "ananya@example.com",
            Role = UserRole.Provider,
            CreatedAt = now
        });
        db.Providers.Add(new Provider
        {
            Id = SeedIds.AnanyaProviderId,
            UserId = SeedIds.AnanyaUserId,
            Status = ProviderStatus.Verified,
            DisplayName = "Ananya Desai",
            Bio = "Teaches Hatha yoga (slow, posture-focused) and restorative yoga (gentle and restful).",
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
        });
        db.Services.Add(new Service
        {
            Id = SeedIds.AnanyaServiceId,
            ProviderId = SeedIds.AnanyaProviderId,
            CategoryId = SeedIds.YogaCategoryId,
            Title = "Yoga session",
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
        await AddWeekAsync(db, time, cancellationToken);
    }

    private static Area Area(Guid id, string name) => new()
    {
        Id = id,
        CityId = CityId,
        Name = name,
        Pincode = "560001",
        IsActive = true
    };

    private static async Task AddWeekAsync(YogaDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var today = MarketClock.Today(time.GetUtcNow());
        var next = today.AddMonths(1);
        var last = new DateOnly(next.Year, next.Month, DateTime.DaysInMonth(next.Year, next.Month));
        for (var date = today; date <= last; date = date.AddDays(1))
        {
            Add(SessionMode.Home, date, new TimeOnly(7, 0), new TimeOnly(8, 0));
            Add(SessionMode.Home, date, new TimeOnly(8, 0), new TimeOnly(9, 0));
            Add(SessionMode.Studio, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            Add(SessionMode.Online, date, new TimeOnly(18, 0), new TimeOnly(19, 0));
            Add(SessionMode.Online, date, new TimeOnly(19, 0), new TimeOnly(20, 0));
        }
        await db.SaveChangesAsync(cancellationToken);

        void Add(SessionMode mode, DateOnly date, TimeOnly start, TimeOnly end) =>
            db.AvailabilitySlots.Add(new AvailabilitySlot
            {
                Id = Guid.NewGuid(),
                ProviderId = SeedIds.AnanyaProviderId,
                Mode = mode,
                Date = date,
                StartTime = start,
                EndTime = end,
                IsBlocked = false
            });
    }
}

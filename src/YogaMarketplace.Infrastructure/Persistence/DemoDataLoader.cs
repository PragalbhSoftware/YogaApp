using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Infrastructure.Persistence;

/// <summary>
/// Loads demo-data.json into a non-Production database. Running it again changes nothing: areas, users, instructors,
/// services and slots are matched on their unique keys, and packs and bookings on an id derived from their JSON key.
/// Bookings go through <see cref="BookingRules"/> with the current admin settings and are paid with a test payment id,
/// so they are only written when payments run in test mode. Existing areas are left as the admin set them.
/// </summary>
public class DemoDataLoader
{
    public const string PathKey = "DemoData:Path";
    public const string TestPaymentPrefix = "pay_test_demo_";
    private const string DefaultPath = "DemoData/demo-data.json";
    private const string TestOrderPrefix = "order_test_demo_";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly BookingStatus[] Occupying =
        Enum.GetValues<BookingStatus>().Where(BookingRules.OccupiesSlot).ToArray();

    private readonly YogaDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _time;
    private int _created;
    private int _updated;

    public DemoDataLoader(YogaDbContext db, IConfiguration config, IHostEnvironment environment, TimeProvider time)
    {
        _db = db;
        _config = config;
        _environment = environment;
        _time = time;
    }

    public async Task<DemoLoadResult> LoadAsync(bool testPayments, CancellationToken cancellationToken = default)
    {
        if (_environment.IsProduction())
            throw new InvalidOperationException("Demo data can't be loaded in Production.");

        var data = await ReadAsync(cancellationToken);
        var now = _time.GetUtcNow();
        _created = 0;
        _updated = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var areas = await UpsertAreasAsync(data.Cities, cancellationToken);
        var customers = await UpsertCustomersAsync(data.Customers, now, cancellationToken);
        var instructors = await UpsertInstructorsAsync(data.Instructors, areas, now, cancellationToken);
        await UpsertPacksAsync(data.Packs, now, cancellationToken);
        await SaveAsync(cancellationToken);

        var skipped = testPayments
            ? await AddBookingsAsync(data.Bookings, customers, instructors, now, cancellationToken)
            : data.Bookings.Count;
        await tx.CommitAsync(cancellationToken);
        return new DemoLoadResult(_created, _updated, skipped);
    }

    private async Task<DemoData> ReadAsync(CancellationToken cancellationToken)
    {
        var path = _config[PathKey] ?? DefaultPath;
        if (!Path.IsPathRooted(path))
        {
            var besideDll = Path.Combine(AppContext.BaseDirectory, path);
            path = File.Exists(besideDll) ? besideDll : Path.Combine(_environment.ContentRootPath, path);
        }
        if (!File.Exists(path))
            throw new DomainException("Demo data file not found.", 404);

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DemoData>(stream, Json, cancellationToken)
            ?? throw new DomainException("Demo data file is empty.");
    }

    private async Task<Dictionary<(string City, string Name), Area>> UpsertAreasAsync(
        List<DemoCity> cities,
        CancellationToken cancellationToken)
    {
        var areas = new Dictionary<(string, string), Area>();
        foreach (var city in cities)
        {
            var place = await EnsureCityAsync(city.Name, cancellationToken);
            foreach (var name in city.Areas)
            {
                var areaName = name.Trim();
                var area = await _db.Areas.SingleOrDefaultAsync(a => a.CityId == place.Id && a.Name == areaName, cancellationToken);
                if (area is null)
                {
                    area = CatalogRules.CreateArea(place.Id, areaName, city.Pin, null, null);
                    _db.Areas.Add(area);
                }
                areas[(place.Name, area.Name)] = area;
            }
        }
        return areas;
    }

    private async Task<Dictionary<string, User>> UpsertCustomersAsync(
        List<DemoCustomer> customers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, User>();
        foreach (var item in customers)
        {
            var user = await UpsertUserAsync(item.Phone, item.Name, item.Gender, UserRole.Customer, now, cancellationToken);
            users[user.Phone] = user;
        }
        return users;
    }

    private async Task<Dictionary<string, (Provider Provider, Service Service)>> UpsertInstructorsAsync(
        List<DemoInstructor> instructors,
        Dictionary<(string City, string Name), Area> areas,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var categories = await _db.Categories.ToDictionaryAsync(c => c.Slug, cancellationToken);
        var result = new Dictionary<string, (Provider, Service)>();
        foreach (var item in instructors)
        {
            var user = await UpsertUserAsync(item.Phone, item.Name, item.Gender, UserRole.Provider, now, cancellationToken);
            var placeName = CatalogRules.NormalizeCityName(item.City);
            if (!areas.TryGetValue((placeName, item.Area.Trim()), out var area))
                throw new DomainException($"Instructor {item.Phone} uses an area that is not in the demo cities.");

            var provider = await _db.Providers.SingleOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (provider is null)
            {
                provider = new Provider { Id = Guid.NewGuid(), UserId = user.Id, User = user, CreatedAt = now };
                _db.Providers.Add(provider);
            }
            provider.DisplayName = item.Name;
            provider.Bio = item.Bio;
            provider.Age = item.Age;
            provider.AreaId = area.Id;
            provider.OffersHome = item.HomeRate is > 0;
            provider.OffersStudio = item.StudioRate is > 0;
            provider.OffersOnline = item.OnlineRate is > 0;
            provider.HomeRate = item.HomeRate;
            provider.StudioRate = item.StudioRate;
            provider.OnlineRate = item.OnlineRate;
            provider.StudioAddress = item.StudioAddress;
            provider.GoogleMeetLink = item.GoogleMeetLink;
            ProviderApproval.Verify(provider, now);

            Service? first = null;
            foreach (var demoService in item.Services)
            {
                if (!categories.TryGetValue(demoService.Category, out var category))
                    throw new DomainException($"Unknown category '{demoService.Category}' in demo data.");
                var service = await _db.Services.SingleOrDefaultAsync(
                    s => s.ProviderId == provider.Id && s.CategoryId == category.Id, cancellationToken);
                if (service is null)
                {
                    service = new Service { Id = Guid.NewGuid(), ProviderId = provider.Id, CategoryId = category.Id };
                    _db.Services.Add(service);
                }
                service.Title = demoService.Title;
                service.IsActive = true;
                first ??= service;
            }
            result[user.Phone] = (provider, first ?? throw new DomainException($"Instructor {item.Phone} needs a service."));
        }
        return result;
    }

    private async Task<User> UpsertUserAsync(
        string phone,
        string name,
        Gender? gender,
        UserRole role,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(phone, out var normalized, out var error))
            throw new DomainException($"{phone}: {error}");

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Phone == normalized, cancellationToken);
        if (user is null)
        {
            user = new User { Id = Guid.NewGuid(), Phone = normalized, Role = role, CreatedAt = now };
            _db.Users.Add(user);
        }
        else if (user.Role != role)
        {
            throw new DomainException($"{normalized} already belongs to a {user.Role} account.", 409);
        }
        user.Name = name;
        user.Gender = gender;
        return user;
    }

    private async Task UpsertPacksAsync(List<DemoPack> packs, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var item in packs)
        {
            CreditPackRules.Validate(item.Name, item.Sessions, item.Price, item.ValidityDays, item.PriceCap);
            var id = StableId("pack", item.Key);
            var pack = await _db.CreditPacks.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (pack is null)
            {
                pack = new CreditPack { Id = id, CreatedAt = now };
                _db.CreditPacks.Add(pack);
            }
            pack.Name = item.Name.Trim();
            pack.Sessions = item.Sessions;
            pack.Price = item.Price;
            pack.ValidityDays = item.ValidityDays;
            pack.PriceCap = item.PriceCap;
            pack.IsActive = true;
            if (_db.Entry(pack).State == EntityState.Modified)
            {
                pack.Version++;
                pack.UpdatedAt = now;
            }
        }
    }

    /// <summary>Returns how many bookings were skipped because their slot is already taken.</summary>
    private async Task<int> AddBookingsAsync(
        List<DemoBooking> bookings,
        Dictionary<string, User> customers,
        Dictionary<string, (Provider Provider, Service Service)> instructors,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await _db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        var terms = new BookingTerms(
            settings.CommissionPercent,
            settings.ConvenienceFee,
            settings.CancelFreeWindowHours,
            settings.LateCancelFeeType,
            settings.LateCancelFeeValue);
        var skipped = 0;

        foreach (var item in bookings)
        {
            var id = StableId("booking", item.Key);
            if (await _db.Bookings.AnyAsync(b => b.Id == id, cancellationToken))
                continue;

            var customer = Lookup(customers, item.Customer, "customer");
            var (provider, service) = Lookup(instructors, item.Instructor, "instructor");
            var amount = provider.RateFor(item.Mode)
                ?? throw new DomainException($"Booking {item.Key}: the instructor has no {item.Mode} rate.");
            var slot = await SlotAsync(provider.Id, item, now, cancellationToken);
            if (await _db.Bookings.AnyAsync(b => b.SlotId == slot.Id && Occupying.Contains(b.Status), cancellationToken))
            {
                skipped++;
                continue;
            }

            var booking = BookingRules.CreateAfterPayment(
                customer.Id, service, slot, amount, terms, item.HomeAddress, item.Landmark, provider.GoogleMeetLink, provider.StudioAddress, now);
            booking.Id = id;
            var payment = new Payment
            {
                Id = StableId("payment", item.Key),
                BookingId = id,
                Amount = decimal.Round(amount + terms.ConvenienceFee, 2, MidpointRounding.AwayFromZero),
                Status = PaymentStatus.Pending,
                Gateway = PaymentGateways.Razorpay,
                GatewayOrderId = TestOrderPrefix + item.Key,
                CreatedAt = now
            };
            PaymentRules.MarkPaid(payment, TestPaymentPrefix + item.Key, now);
            booking.Payment = payment;
            _db.Bookings.Add(booking);
            ApplyStatus(booking, item, now);
            await SaveAsync(cancellationToken);
        }
        return skipped;
    }

    private void ApplyStatus(Booking booking, DemoBooking item, DateTimeOffset now)
    {
        switch (item.Status)
        {
            case BookingStatus.PendingAccept:
                return;
            case BookingStatus.Upcoming:
                BookingRules.Accept(booking, now);
                return;
            case BookingStatus.Completed:
                BookingRules.Accept(booking, now);
                BookingRules.Complete(booking, now);
                _db.PayoutsPending.Add(PayoutCalculator.ForCompletedBooking(booking, now));
                return;
            default:
                throw new DomainException($"Booking {item.Key}: demo bookings can be PendingAccept, Upcoming or Completed.");
        }
    }

    private async Task<AvailabilitySlot> SlotAsync(Guid providerId, DemoBooking item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var date = MarketClock.Today(now).AddDays(item.DayOffset);
        var slot = await _db.AvailabilitySlots.SingleOrDefaultAsync(
            s => s.ProviderId == providerId && s.Mode == item.Mode && s.Date == date && s.StartTime == item.Start,
            cancellationToken);
        if (slot is not null)
            return slot;

        slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Mode = item.Mode,
            Date = date,
            StartTime = item.Start,
            EndTime = item.End
        };
        _db.AvailabilitySlots.Add(slot);
        return slot;
    }

    private static T Lookup<T>(Dictionary<string, T> byPhone, string phone, string label)
    {
        if (PhoneNumber.TryNormalize(phone, out var normalized, out _) && byPhone.TryGetValue(normalized, out var found))
            return found;
        throw new DomainException($"Demo booking refers to an unknown {label} {phone}.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        _db.ChangeTracker.DetectChanges();
        foreach (var entry in _db.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
                _created++;
            else if (entry.State == EntityState.Modified)
                _updated++;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static Guid StableId(string kind, string key) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"demo:{kind}:{key}")).AsSpan(0, 16));

    private sealed record DemoData(
        List<DemoCity> Cities,
        List<DemoCustomer> Customers,
        List<DemoInstructor> Instructors,
        List<DemoPack> Packs,
        List<DemoBooking> Bookings);

    private async Task<City> EnsureCityAsync(string rawName, CancellationToken cancellationToken)
    {
        var name = CatalogRules.NormalizeCityName(rawName);
        var existing = _db.Cities.Local.FirstOrDefault(c => c.Name == name)
            ?? await _db.Cities.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
        if (existing is not null)
            return existing;

        var state = _db.States.Local.FirstOrDefault(s => s.Code == "DM")
            ?? await _db.States.FirstOrDefaultAsync(s => s.Code == "DM", cancellationToken);
        if (state is null)
        {
            state = CatalogRules.CreateState("Demo region", "DM", 0);
            _db.States.Add(state);
        }

        var city = CatalogRules.CreateCity(name, state.Id, state.Code, 0);
        _db.Cities.Add(city);
        return city;
    }

    private sealed record DemoCity(string Name, string Pin, List<string> Areas);

    private sealed record DemoCustomer(string Phone, string Name, Gender? Gender);

    private sealed record DemoInstructor(
        string Phone,
        string Name,
        Gender? Gender,
        string? Bio,
        int? Age,
        string City,
        string Area,
        decimal? HomeRate,
        decimal? StudioRate,
        decimal? OnlineRate,
        string? StudioAddress,
        string? GoogleMeetLink,
        List<DemoService> Services);

    private sealed record DemoService(string Category, string Title);

    private sealed record DemoPack(string Key, string Name, int Sessions, decimal Price, int ValidityDays, decimal PriceCap);

    private sealed record DemoBooking(
        string Key,
        string Customer,
        string Instructor,
        SessionMode Mode,
        int DayOffset,
        TimeOnly Start,
        TimeOnly End,
        BookingStatus Status,
        string? HomeAddress,
        string? Landmark);
}

public sealed record DemoLoadResult(int Created, int Updated, int BookingsSkipped);

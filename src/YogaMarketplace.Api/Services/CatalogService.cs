using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public class CatalogService
{
    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;

    public CatalogService(YogaDbContext db, IPlatformSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<IReadOnlyList<AreaResponse>> AreasAsync(Guid? cityId, CancellationToken cancellationToken)
    {
        var query = _db.Areas.AsNoTracking()
            .Where(a => a.IsActive && a.City!.IsActive && a.City.State!.IsActive);
        if (cityId is Guid id && id != Guid.Empty)
            query = query.Where(a => a.CityId == id);
        return await query
            .OrderBy(a => a.City!.Name)
            .ThenBy(a => a.Name)
            .Select(a => new AreaResponse(a.Id, a.CityId, a.City!.Name, a.City.StateId, a.City.State!.Name, a.Pincode, a.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StateResponse>> StatesAsync(CancellationToken cancellationToken) =>
        await _db.States.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => new StateResponse(s.Id, s.Name, s.Code, s.DisplayOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CityResponse>> CitiesAsync(Guid? stateId, CancellationToken cancellationToken)
    {
        var query = _db.Cities.AsNoTracking().Where(c => c.IsActive && c.State!.IsActive);
        if (stateId is Guid id && id != Guid.Empty)
            query = query.Where(c => c.StateId == id);

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CityResponse(c.Id, c.Name, c.StateId, c.Slug, c.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The active city whose active areas, those with both coordinates, have the closest average point.
    /// Null when no such city exists.
    /// </summary>
    public async Task<CityResponse?> NearestCityAsync(double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        if (latitude is not double lat || longitude is not double lng)
            throw new DomainException("Enter a latitude and a longitude.");
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            throw new DomainException("Latitude must be between -90 and 90, and longitude between -180 and 180.");

        var areas = await _db.Areas.AsNoTracking()
            .Where(a => a.IsActive && a.Latitude != null && a.Longitude != null && a.City!.IsActive && a.City.State!.IsActive)
            .Select(a => new
            {
                a.CityId,
                a.Latitude,
                a.Longitude,
                a.City!.Name,
                a.City.StateId,
                a.City.Slug,
                a.City.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        CityResponse? nearest = null;
        var best = double.MaxValue;
        foreach (var city in areas.GroupBy(a => a.CityId))
        {
            var sample = city.First();
            var averageLat = city.Average(a => (double)a.Latitude!.Value);
            var averageLng = city.Average(a => (double)a.Longitude!.Value);
            var distance = HaversineKm(lat, lng, averageLat, averageLng);
            if (nearest is not null && (distance > best || (distance == best && string.Compare(sample.Name, nearest.Name, StringComparison.Ordinal) >= 0)))
                continue;
            best = distance;
            nearest = new CityResponse(sample.CityId, sample.Name, sample.StateId, sample.Slug, sample.DisplayOrder);
        }

        return nearest;
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371;
        var lat = Radians(lat2 - lat1);
        var lng = Radians(lng2 - lng1);
        var a = Math.Sin(lat / 2) * Math.Sin(lat / 2)
            + Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Sin(lng / 2) * Math.Sin(lng / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    public async Task<IReadOnlyList<CategoryResponse>> CategoriesAsync(CancellationToken cancellationToken) =>
        await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);

    public async Task<PolicyResponse> PolicyAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var terms = settings.Terms;
        return new PolicyResponse(
            settings.Currency,
            terms.CommissionPercent,
            terms.ConvenienceFee,
            terms.CancelFreeWindowHours,
            settings.RescheduleFreeWindowHours,
            terms.LateCancelFeeType.ToString(),
            terms.LateCancelFeeValue);
    }

    public async Task<BannerResponse> BannerAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        return new BannerResponse(settings.BannerTitle, settings.BannerSubtitle, settings.BannerOffer);
    }
}

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
        if (stateId is not Guid id || id == Guid.Empty)
            throw new DomainException("Pick a state.");

        return await _db.Cities.AsNoTracking()
            .Where(c => c.IsActive && c.StateId == id && c.State!.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CityResponse(c.Id, c.Name, c.StateId, c.Slug, c.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

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

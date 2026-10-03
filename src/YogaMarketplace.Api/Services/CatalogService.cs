using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<AreaResponse>> AreasAsync(CancellationToken cancellationToken) =>
        await _db.Areas.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.City)
            .ThenBy(a => a.Name)
            .Select(a => new AreaResponse(a.Id, a.City, a.Name))
            .ToListAsync(cancellationToken);

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

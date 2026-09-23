using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public class CatalogService
{
    private readonly YogaDbContext _db;

    public CatalogService(YogaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AreaResponse>> AreasAsync(CancellationToken cancellationToken) =>
        await _db.Areas.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
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
        var policy = await _db.Policies.AsNoTracking().SingleAsync(cancellationToken);
        return new PolicyResponse(
            policy.Currency,
            policy.PlatformFeePercent,
            policy.CancelFreeWindowHours,
            policy.RescheduleFreeWindowHours,
            policy.LateCancelFeePercent,
            policy.PolicyNote);
    }
}

using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminMasterService
{
    Task<IReadOnlyList<AdminAreaResponse>> ListAreasAsync(CancellationToken cancellationToken);

    Task<AdminAreaResponse> CreateAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken);

    Task<AdminAreaResponse> UpdateAreaAsync(Guid id, PatchAreaRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<AdminCategoryResponse> UpdateCategoryAsync(Guid id, PatchCategoryRequest request, CancellationToken cancellationToken);
}

public class AdminMasterService : IAdminMasterService
{
    private readonly YogaDbContext _db;

    public AdminMasterService(YogaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminAreaResponse>> ListAreasAsync(CancellationToken cancellationToken) =>
        await _db.Areas.AsNoTracking()
            .OrderBy(a => a.City)
            .ThenBy(a => a.Name)
            .Select(a => new AdminAreaResponse(a.Id, a.City, a.Name, a.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminAreaResponse> CreateAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken)
    {
        var area = CatalogRules.CreateArea(request.City, request.Name);
        var cityOpen = await _db.Areas.AnyAsync(a => a.City == area.City, cancellationToken);
        if (!cityOpen)
            throw new DomainException("Pick one of the existing cities. Opening new cities is not available yet.");
        await EnsureAreaNameAvailableAsync(area.City, area.Name, null, cancellationToken);
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(cancellationToken);
        return ToArea(area);
    }

    public async Task<AdminAreaResponse> UpdateAreaAsync(Guid id, PatchAreaRequest request, CancellationToken cancellationToken)
    {
        if (request.Name is null && request.IsActive is null)
            throw new DomainException("Send a name or isActive.");

        var area = await _db.Areas.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new DomainException("Area not found.", 404);

        if (request.Name is not null)
        {
            CatalogRules.RenameArea(area, request.Name);
            await EnsureAreaNameAvailableAsync(area.City, area.Name, area.Id, cancellationToken);
        }

        if (request.IsActive is bool active)
            area.IsActive = active;

        await _db.SaveChangesAsync(cancellationToken);
        return ToArea(area);
    }

    public async Task<IReadOnlyList<AdminCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryResponse(c.Id, c.Name, c.Slug, c.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AdminCategoryResponse> UpdateCategoryAsync(
        Guid id,
        PatchCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("Unknown category.", 404);
        var slug = category.Slug;
        CatalogRules.RenameCategory(category, request.Name);
        category.Slug = slug;
        await _db.SaveChangesAsync(cancellationToken);
        return new AdminCategoryResponse(category.Id, category.Name, category.Slug, category.IsActive);
    }

    private async Task EnsureAreaNameAvailableAsync(string city, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var matches = _db.Areas.Where(a => a.City == city && a.Name.ToLower() == lowered);
        if (exceptId is Guid id)
            matches = matches.Where(a => a.Id != id);
        var taken = await matches.AnyAsync(cancellationToken);
        if (taken)
            throw new DomainException("That area already exists.", 409);
    }

    private static AdminAreaResponse ToArea(Area area) => new(area.Id, area.City, area.Name, area.IsActive);
}

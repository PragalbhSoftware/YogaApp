using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public class ContentPageService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ILogger<ContentPageService> _logger;

    public ContentPageService(YogaDbContext db, ICurrentUser current, ILogger<ContentPageService> logger)
    {
        _db = db;
        _current = current;
        _logger = logger;
    }

    public async Task<ContentPageResponse> GetAsync(string slug, CancellationToken cancellationToken)
    {
        var page = await FindAsync(slug, tracking: false, cancellationToken);
        return ToResponse(page);
    }

    public async Task<IReadOnlyList<ContentPageResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var pages = await _db.ContentPages.AsNoTracking().ToListAsync(cancellationToken);
        return pages
            .OrderBy(p => ContentPageRules.Slugs.ToList().IndexOf(p.Slug))
            .Select(ToResponse)
            .ToList();
    }

    public async Task<ContentPageResponse> UpdateAsync(string slug, UpdateContentPageRequest request, CancellationToken cancellationToken)
    {
        if (request.Version is not int expected)
            throw new DomainException("Send the page version you edited.");

        var page = await FindAsync(slug, tracking: true, cancellationToken);
        if (page.Version != expected)
            throw new DomainException(ContentPageRules.StaleMessage, 409);

        _db.Entry(page).Property(p => p.Version).OriginalValue = expected;
        if (!ContentPageRules.Apply(page, request.Title, request.Body, _current.UserId, DateTimeOffset.UtcNow))
            return ToResponse(page);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(ContentPageRules.StaleMessage, 409);
        }

        _logger.LogInformation("Admin {AdminId} saved page {Slug} as version {Version}.", _current.UserId, page.Slug, page.Version);
        return ToResponse(page);
    }

    private async Task<ContentPage> FindAsync(string slug, bool tracking, CancellationToken cancellationToken)
    {
        if (!ContentPageRules.IsKnown(slug))
            throw new DomainException("Page not found.", 404);
        var query = tracking ? _db.ContentPages : _db.ContentPages.AsNoTracking();
        return await query.SingleOrDefaultAsync(p => p.Slug == slug, cancellationToken)
            ?? throw new DomainException("Page not found.", 404);
    }

    private static ContentPageResponse ToResponse(ContentPage page) =>
        new(page.Slug, page.Title, page.Body, page.Version, page.UpdatedAt);
}

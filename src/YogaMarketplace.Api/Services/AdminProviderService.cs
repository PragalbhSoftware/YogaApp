using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminProviderService
{
    Task<IReadOnlyList<AdminProviderResponse>> ListAsync(string? status, CancellationToken cancellationToken);

    Task<AdminProviderResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminProviderResponse> VerifyAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminProviderResponse> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken);
}

public class AdminProviderService : IAdminProviderService
{
    private readonly YogaDbContext _db;

    public AdminProviderService(YogaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminProviderResponse>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var query = Providers();
        if (string.IsNullOrWhiteSpace(status) || status.Trim().Equals("pending", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.Status == ProviderStatus.Pending);
        else if (!status.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = AdminQuery.ParseRequired<ProviderStatus>(status, "Unknown provider status.");
            query = query.Where(p => p.Status == parsed);
        }

        var providers = await query.ToListAsync(cancellationToken);
        return providers
            .OrderByDescending(p => p.CreatedAt)
            .Take(AdminQuery.PageSize)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<AdminProviderResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var provider = await Providers().SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new DomainException("Instructor not found.", 404);
        return ToResponse(provider);
    }

    public Task<AdminProviderResponse> VerifyAsync(Guid id, CancellationToken cancellationToken) =>
        ReviewAsync(id, ProviderApproval.Verify, cancellationToken);

    public Task<AdminProviderResponse> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken) =>
        ReviewAsync(id, provider => ProviderApproval.Reject(provider, reason), cancellationToken);

    private async Task<AdminProviderResponse> ReviewAsync(
        Guid id,
        Action<Provider> review,
        CancellationToken cancellationToken)
    {
        var provider = await _db.Providers
            .Include(p => p.User)
            .Include(p => p.Area)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new DomainException("Instructor not found.", 404);

        review(provider);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(provider);
    }

    private IQueryable<Provider> Providers() =>
        _db.Providers.AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Area);

    internal static AdminProviderResponse ToResponse(Provider provider)
    {
        var user = provider.User ?? throw new InvalidOperationException("Provider user was not loaded.");
        var area = provider.Area ?? throw new InvalidOperationException("Provider area was not loaded.");
        return new AdminProviderResponse(
            provider.Id,
            provider.UserId,
            provider.DisplayName,
            provider.Bio,
            provider.Age,
            user.Email,
            user.Phone,
            provider.AreaId,
            area.Name,
            area.City,
            provider.Status.ToString(),
            provider.OffersHome,
            provider.OffersStudio,
            provider.OffersOnline,
            provider.HomeRate,
            provider.StudioRate,
            provider.OnlineRate,
            provider.StudioAddress,
            provider.GoogleMeetLink,
            provider.RejectionReason,
            provider.CreatedAt,
            provider.ReviewedAt);
    }
}

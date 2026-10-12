using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>
/// The instructor's services and trial offer. The instructor picks which active service carries the trial; at most one
/// service per instructor has it.
/// </summary>
public class ProviderTrialService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPlatformSettingsService _settings;

    public ProviderTrialService(YogaDbContext db, ICurrentUser current, IPlatformSettingsService settings)
    {
        _db = db;
        _current = current;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ProviderServiceResponse>> ListServicesAsync(CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var categories = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return provider.Services
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Title, StringComparer.Ordinal)
            .Select(s => new ProviderServiceResponse(
                s.Id,
                s.Title,
                categories.GetValueOrDefault(s.CategoryId, ""),
                s.IsActive,
                TrialRules.Offers(s),
                s.TrialPrice))
            .ToList();
    }

    public async Task<ProviderTrialResponse> GetAsync(CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        return await ToResponseAsync(provider, cancellationToken);
    }

    public async Task<ProviderTrialResponse> UpdateAsync(UpdateProviderTrialRequest request, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var services = provider.Services.Where(s => s.IsActive).ToList();
        var target = PickService(services, request.ServiceId);

        foreach (var other in services.Where(s => s.Id != target.Id))
            TrialRules.Disable(other);

        if (request.Enabled)
        {
            var price = request.Price ?? throw new DomainException("Enter a trial price. It can be 0.");
            var settings = await _settings.GetAsync(cancellationToken);
            TrialRules.Enable(target, TrialRules.CleanPrice(price, provider.LowestOfferedRate(), settings.Terms.ConvenienceFee));
        }
        else
        {
            TrialRules.Disable(target);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(provider, cancellationToken);
    }

    /// <summary>The requested service, else the one that has the trial now, else the first by title.</summary>
    private static Service PickService(List<Service> active, Guid? serviceId)
    {
        if (serviceId is Guid id && id != Guid.Empty)
        {
            return active.SingleOrDefault(s => s.Id == id)
                ?? throw new DomainException("Pick one of your active services.");
        }
        return active.FirstOrDefault(TrialRules.Offers)
            ?? active.OrderBy(s => s.Title, StringComparer.Ordinal).FirstOrDefault()
            ?? throw new DomainException("You have no bookable service yet.", 409);
    }

    private async Task<ProviderTrialResponse> ToResponseAsync(Provider provider, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var trial = provider.Services.FirstOrDefault(TrialRules.Offers);
        var price = trial?.TrialPrice ?? provider.Services.Select(s => s.TrialPrice).FirstOrDefault(p => p is not null);
        return new ProviderTrialResponse(trial?.Id, trial is not null, price, provider.LowestOfferedRate(), settings.Terms.ConvenienceFee);
    }

    private async Task<Provider> RequireMineAsync(CancellationToken cancellationToken) =>
        await _db.Providers.Include(p => p.Services).SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
        ?? throw new DomainException("Only instructors can do that.", 403);
}

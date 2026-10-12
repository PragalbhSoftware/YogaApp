using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>The instructor's trial offer. It lives on their main service, so at most one service per instructor has it.</summary>
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

    public async Task<ProviderTrialResponse> GetAsync(CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        return await ToResponseAsync(provider, cancellationToken);
    }

    public async Task<ProviderTrialResponse> UpdateAsync(UpdateProviderTrialRequest request, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var services = provider.Services.Where(s => s.IsActive).ToList();
        var main = services.OrderBy(s => s.Title).FirstOrDefault()
            ?? throw new DomainException("You have no bookable service yet.", 409);

        foreach (var other in services.Where(s => s.Id != main.Id))
            TrialRules.Disable(other);

        if (request.Enabled)
        {
            var price = request.Price ?? throw new DomainException("Enter a trial price. It can be 0.");
            var settings = await _settings.GetAsync(cancellationToken);
            TrialRules.Enable(main, TrialRules.CleanPrice(price, provider.LowestOfferedRate(), settings.Terms.ConvenienceFee));
        }
        else
        {
            TrialRules.Disable(main);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(provider, cancellationToken);
    }

    private async Task<ProviderTrialResponse> ToResponseAsync(Provider provider, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var trial = provider.Services.FirstOrDefault(TrialRules.Offers);
        var price = trial?.TrialPrice ?? provider.Services.Select(s => s.TrialPrice).FirstOrDefault(p => p is not null);
        return new ProviderTrialResponse(trial is not null, price, provider.LowestOfferedRate(), settings.Terms.ConvenienceFee);
    }

    private async Task<Provider> RequireMineAsync(CancellationToken cancellationToken) =>
        await _db.Providers.Include(p => p.Services).SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
        ?? throw new DomainException("Only instructors can do that.", 403);
}

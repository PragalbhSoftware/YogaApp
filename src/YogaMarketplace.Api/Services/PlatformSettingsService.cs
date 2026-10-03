using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>Read-only copy of <see cref="PlatformSettings"/> that is safe to share from the cache.</summary>
public sealed record CurrentSettings(
    string Currency,
    BookingTerms Terms,
    int RescheduleFreeWindowHours,
    PayoutCycle PayoutCycle,
    string PolicyNote,
    string? BannerTitle,
    string? BannerSubtitle,
    string? BannerOffer,
    int Version,
    DateTimeOffset? UpdatedAt)
{
    public static CurrentSettings From(PlatformSettings settings) => new(
        settings.Currency,
        PlatformSettingsRules.TermsFor(settings),
        settings.RescheduleFreeWindowHours,
        settings.PayoutCycle,
        settings.PolicyNote,
        settings.BannerTitle,
        settings.BannerSubtitle,
        settings.BannerOffer,
        settings.Version,
        settings.UpdatedAt);
}

public interface IPlatformSettingsService
{
    Task<CurrentSettings> GetAsync(CancellationToken cancellationToken);

    Task<AdminSettingsResponse> GetForAdminAsync(CancellationToken cancellationToken);

    Task<AdminSettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingsAuditResponse>> ListAuditAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Caches the settings row per API instance. A save clears this instance's cache at once; other instances
/// pick the change up when their entry expires after <see cref="CacheLifetime"/>.
/// </summary>
public class PlatformSettingsService : IPlatformSettingsService
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);
    private const string CacheKey = "platform-settings";
    private const int AuditPageSize = 50;
    private const string StaleMessage = "Another admin saved these settings first. Reload to see the latest values.";

    private readonly YogaDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _current;
    private readonly ILogger<PlatformSettingsService> _logger;

    public PlatformSettingsService(
        YogaDbContext db,
        IMemoryCache cache,
        ICurrentUser current,
        ILogger<PlatformSettingsService> logger)
    {
        _db = db;
        _cache = cache;
        _current = current;
        _logger = logger;
    }

    public async Task<CurrentSettings> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out CurrentSettings? cached) && cached is not null)
            return cached;

        var row = await _db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        var current = CurrentSettings.From(row);
        _cache.Set(CacheKey, current, CacheLifetime);
        return current;
    }

    public async Task<AdminSettingsResponse> GetForAdminAsync(CancellationToken cancellationToken) =>
        ToAdmin(await _db.Settings.AsNoTracking().SingleAsync(cancellationToken));

    public async Task<AdminSettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.Version is not int expected)
            throw new DomainException("Send the settings version you edited.");

        var settings = await _db.Settings.SingleAsync(cancellationToken);
        if (settings.Version != expected)
            throw new DomainException(StaleMessage, 409);

        var changes = PlatformSettingsRules.Apply(settings, ToChanges(request));
        if (changes.Count == 0)
            return ToAdmin(settings);

        var now = DateTimeOffset.UtcNow;
        _db.Entry(settings).Property(s => s.Version).OriginalValue = expected;
        settings.Version = expected + 1;
        settings.UpdatedAt = now;
        _db.SettingsAudits.AddRange(changes.Select(change => new SettingsAudit
        {
            Id = Guid.NewGuid(),
            AdminUserId = _current.UserId,
            ChangedAt = now,
            Field = change.Field,
            OldValue = change.OldValue,
            NewValue = change.NewValue
        }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(StaleMessage, 409);
        }
        finally
        {
            _cache.Remove(CacheKey);
        }

        _logger.LogInformation(
            "Admin {AdminId} changed settings to version {Version}: {Fields}.",
            _current.UserId,
            settings.Version,
            string.Join(", ", changes.Select(c => c.Field)));
        return ToAdmin(settings);
    }

    public async Task<IReadOnlyList<SettingsAuditResponse>> ListAuditAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.SettingsAudits.AsNoTracking()
            .Join(_db.Users.AsNoTracking(), a => a.AdminUserId, u => u.Id, (a, u) => new { Audit = a, u.Name })
            .ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(r => r.Audit.ChangedAt)
            .ThenBy(r => r.Audit.Field, StringComparer.Ordinal)
            .Take(AuditPageSize)
            .Select(r => new SettingsAuditResponse(
                r.Audit.Id,
                r.Audit.ChangedAt,
                r.Audit.AdminUserId,
                r.Name,
                r.Audit.Field,
                r.Audit.OldValue,
                r.Audit.NewValue))
            .ToList();
    }

    private static PlatformSettingsChanges ToChanges(UpdateSettingsRequest request) => new(
        request.CommissionPercent,
        request.ConvenienceFee,
        request.CancelFreeWindowHours,
        request.RescheduleFreeWindowHours,
        AdminQuery.ParseOptional<LateCancelFeeType>(request.LateCancelFeeType, "Late-cancel fee type must be Percent or Flat."),
        request.LateCancelFeeValue,
        AdminQuery.ParseOptional<PayoutCycle>(request.PayoutCycle, "Payout cycle must be Weekly or Biweekly."),
        request.PolicyNote,
        request.BannerTitle,
        request.BannerSubtitle,
        request.BannerOffer);

    private static AdminSettingsResponse ToAdmin(PlatformSettings settings) => new(
        settings.Version,
        settings.Currency,
        settings.CommissionPercent,
        settings.ConvenienceFee,
        settings.CancelFreeWindowHours,
        settings.RescheduleFreeWindowHours,
        settings.LateCancelFeeType.ToString(),
        settings.LateCancelFeeValue,
        settings.PayoutCycle.ToString(),
        PayoutCycleRules.CurrentPeriodStart(settings.PayoutCycle, DateTimeOffset.UtcNow),
        settings.PolicyNote,
        settings.BannerTitle,
        settings.BannerSubtitle,
        settings.BannerOffer,
        settings.UpdatedAt);
}

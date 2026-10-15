using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>Records Terms and Privacy acceptance against the policy version in admin settings.</summary>
public class ConsentService
{
    private readonly YogaDbContext _db;
    private readonly IPlatformSettingsService _settings;
    private readonly byte[] _pepper;
    private readonly TimeProvider _time;

    public ConsentService(YogaDbContext db, IPlatformSettingsService settings, IOptions<OtpOptions> otp, TimeProvider time)
    {
        _db = db;
        _settings = settings;
        _pepper = Encoding.UTF8.GetBytes(otp.Value.Pepper ?? "");
        _time = time;
    }

    /// <summary>Adds records for the current version to the context without saving. Used when the account is created.</summary>
    public async Task AddAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var version = (await _settings.GetAsync(cancellationToken)).PolicyVersion;
        Add(userId, ConsentRules.Purposes, version, ipAddress);
    }

    public async Task<bool> IsRequiredAsync(Guid userId, CancellationToken cancellationToken)
    {
        var version = (await _settings.GetAsync(cancellationToken)).PolicyVersion;
        var records = await _db.ConsentRecords.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        return ConsentRules.IsRequired(records, version);
    }

    /// <summary>Records the current version for every purpose not yet accepted. Repeating it stores nothing new.</summary>
    public async Task AcceptAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var version = (await _settings.GetAsync(cancellationToken)).PolicyVersion;
        var accepted = await _db.ConsentRecords.AsNoTracking()
            .Where(c => c.UserId == userId && c.PolicyVersion == version)
            .Select(c => c.Purpose)
            .ToListAsync(cancellationToken);
        if (accepted.Count == ConsentRules.Purposes.Count)
            return;

        Add(userId, ConsentRules.Purposes.Except(accepted), version, ipAddress);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A second tab may have accepted the same version at the same moment; anything else is a real failure.
            _db.ChangeTracker.Clear();
            var count = await _db.ConsentRecords.CountAsync(c => c.UserId == userId && c.PolicyVersion == version, cancellationToken);
            if (count != ConsentRules.Purposes.Count)
                throw;
        }
    }

    private void Add(Guid userId, IEnumerable<ConsentPurpose> purposes, int version, string? ipAddress)
    {
        var now = _time.GetUtcNow();
        var ipHash = HashIp(ipAddress);
        _db.ConsentRecords.AddRange(purposes.Select(purpose => new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            PolicyVersion = version,
            AcceptedAt = now,
            IpHash = ipHash
        }));
    }

    private string? HashIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;
        return Convert.ToHexString(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(ipAddress))).ToLowerInvariant();
    }
}

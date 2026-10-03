using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using YogaMarketplace.Api.Options;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Security;

public record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues, rotates, and revokes refresh tokens. The raw token only ever leaves the server in the
/// httpOnly cookie; the database keeps its SHA-256 hash.
/// </summary>
public class RefreshTokenService
{
    private const string SessionEnded = "Your session has ended. Sign in again.";

    private readonly YogaDbContext _db;
    private readonly RefreshTokenOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(YogaDbContext db, IOptions<RefreshTokenOptions> options, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    private TimeSpan Lifetime => TimeSpan.FromDays(_options.LifetimeDays);

    /// <summary>Starts a new token family for a fresh sign-in.</summary>
    public async Task<IssuedRefreshToken> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var value = NewValue();
        var token = RefreshTokenRules.Issue(user.Id, Guid.NewGuid(), Hash(value), DateTimeOffset.UtcNow, Lifetime);
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(value, token.ExpiresAt);
    }

    /// <summary>
    /// Swaps a valid token for a new one. Throws 401 for unknown, expired, revoked, or reused tokens
    /// (reuse also revokes the family), 409 when another request rotated it moments ago, and 403
    /// for a blocked user (all their tokens are revoked).
    /// </summary>
    public async Task<(User User, IssuedRefreshToken Token)> RotateAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(SessionEnded, 401);

        var hash = Hash(value);
        var current = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new DomainException(SessionEnded, 401);

        var now = DateTimeOffset.UtcNow;
        switch (RefreshTokenRules.StateOf(current, now, TimeSpan.FromSeconds(_options.ReuseGraceSeconds)))
        {
            case RefreshTokenState.JustRotated:
                throw new DomainException("Your session was just refreshed. Try again.", 409);
            case RefreshTokenState.Reused:
                _logger.LogWarning("Refresh token reuse for user {UserId}; revoking family {FamilyId}.", current.UserId, current.FamilyId);
                await RevokeFamilyAsync(current.FamilyId, RefreshTokenRevokeReason.Reused, now, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                throw new DomainException(SessionEnded, 401);
            case RefreshTokenState.Expired:
            case RefreshTokenState.Revoked:
                throw new DomainException(SessionEnded, 401);
        }

        var user = current.User ?? throw new DomainException(SessionEnded, 401);
        if (user.IsBlocked)
        {
            await RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.Blocked, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            UserBlocking.EnsureCanSignIn(user);
        }

        var nextValue = NewValue();
        var next = RefreshTokenRules.Rotate(current, Hash(nextValue), now, Lifetime);
        _db.RefreshTokens.Add(next);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("Your session was just refreshed. Try again.", 409);
        }
        return (user, new IssuedRefreshToken(nextValue, next.ExpiresAt));
    }

    /// <summary>Sign-out: revokes every token from the same sign-in. Unknown tokens are ignored.</summary>
    public async Task SignOutAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        var hash = Hash(value);
        var familyId = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (familyId is null)
            return;
        await RevokeFamilyAsync(familyId.Value, RefreshTokenRevokeReason.SignedOut, DateTimeOffset.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Marks every active token of the user revoked. The caller saves.</summary>
    public async Task RevokeAllForUserAsync(Guid userId, RefreshTokenRevokeReason reason, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
            RefreshTokenRules.Revoke(token, reason, now);
    }

    private async Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevokeReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
            RefreshTokenRules.Revoke(token, reason, now);
    }

    private static string NewValue() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

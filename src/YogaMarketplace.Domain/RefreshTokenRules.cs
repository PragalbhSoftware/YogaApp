namespace YogaMarketplace.Domain;

public enum RefreshTokenState
{
    Active,
    Expired,
    Revoked,
    /// <summary>Rotated moments ago, usually by another tab racing this one. Not treated as theft.</summary>
    JustRotated,
    /// <summary>A rotated token came back after the grace period: assume it was stolen.</summary>
    Reused
}

/// <summary>
/// Refresh-token rotation. Each refresh revokes the presented token and issues a new one in the
/// same family. Presenting an already-rotated token after <c>grace</c> revokes the whole family.
/// </summary>
public static class RefreshTokenRules
{
    public static RefreshTokenState StateOf(RefreshToken token, DateTimeOffset now, TimeSpan grace)
    {
        if (token.RevokedAt is { } revokedAt)
        {
            if (token.RevokeReason != RefreshTokenRevokeReason.Rotated)
                return RefreshTokenState.Revoked;
            return now - revokedAt <= grace ? RefreshTokenState.JustRotated : RefreshTokenState.Reused;
        }
        return token.ExpiresAt <= now ? RefreshTokenState.Expired : RefreshTokenState.Active;
    }

    public static RefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FamilyId = familyId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now + lifetime
    };

    /// <summary>Revokes <paramref name="current"/> and returns its replacement in the same family.</summary>
    public static RefreshToken Rotate(RefreshToken current, string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var next = Issue(current.UserId, current.FamilyId, newTokenHash, now, lifetime);
        Revoke(current, RefreshTokenRevokeReason.Rotated, now);
        current.ReplacedById = next.Id;
        return next;
    }

    /// <summary>No-op for a token that is already revoked, so the first reason is kept.</summary>
    public static void Revoke(RefreshToken token, RefreshTokenRevokeReason reason, DateTimeOffset now)
    {
        if (token.RevokedAt is not null)
            return;
        token.RevokedAt = now;
        token.RevokeReason = reason;
    }
}

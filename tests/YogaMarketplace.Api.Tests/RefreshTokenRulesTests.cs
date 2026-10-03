using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class RefreshTokenRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    [Fact]
    public void A_new_token_is_active_until_it_expires()
    {
        var token = RefreshTokenRules.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Lifetime);
        Assert.Equal(Now + Lifetime, token.ExpiresAt);
        Assert.Equal(RefreshTokenState.Active, RefreshTokenRules.StateOf(token, Now, Grace));
        Assert.Equal(RefreshTokenState.Expired, RefreshTokenRules.StateOf(token, token.ExpiresAt, Grace));
    }

    [Fact]
    public void Rotation_keeps_the_family_and_links_the_replacement()
    {
        var first = RefreshTokenRules.Issue(Guid.NewGuid(), Guid.NewGuid(), "one", Now, Lifetime);
        var second = RefreshTokenRules.Rotate(first, "two", Now.AddMinutes(10), Lifetime);

        Assert.Equal(first.FamilyId, second.FamilyId);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(second.Id, first.ReplacedById);
        Assert.Equal(RefreshTokenRevokeReason.Rotated, first.RevokeReason);
        Assert.Equal(Now.AddMinutes(10) + Lifetime, second.ExpiresAt);
        Assert.Equal(RefreshTokenState.Active, RefreshTokenRules.StateOf(second, Now.AddMinutes(10), Grace));
    }

    [Fact]
    public void A_rotated_token_is_a_tab_race_inside_the_grace_and_reuse_after_it()
    {
        var first = RefreshTokenRules.Issue(Guid.NewGuid(), Guid.NewGuid(), "one", Now, Lifetime);
        RefreshTokenRules.Rotate(first, "two", Now, Lifetime);

        Assert.Equal(RefreshTokenState.JustRotated, RefreshTokenRules.StateOf(first, Now + Grace, Grace));
        Assert.Equal(RefreshTokenState.Reused, RefreshTokenRules.StateOf(first, Now + Grace + TimeSpan.FromSeconds(1), Grace));
    }

    [Fact]
    public void Signed_out_or_blocked_tokens_are_revoked_and_keep_the_first_reason()
    {
        var token = RefreshTokenRules.Issue(Guid.NewGuid(), Guid.NewGuid(), "one", Now, Lifetime);
        RefreshTokenRules.Revoke(token, RefreshTokenRevokeReason.Blocked, Now);
        RefreshTokenRules.Revoke(token, RefreshTokenRevokeReason.SignedOut, Now.AddMinutes(1));

        Assert.Equal(RefreshTokenRevokeReason.Blocked, token.RevokeReason);
        Assert.Equal(Now, token.RevokedAt);
        Assert.Equal(RefreshTokenState.Revoked, RefreshTokenRules.StateOf(token, Now, Grace));
    }
}

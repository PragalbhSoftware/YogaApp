using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Tests;

public class UserBlockingRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Block_needs_a_reason_and_records_who_blocked_and_why()
    {
        var user = new User { Id = Guid.NewGuid(), Role = UserRole.Customer };

        Assert.Throws<DomainException>(() => UserBlocking.Block(user, AdminId, null, Now));
        Assert.Throws<DomainException>(() => UserBlocking.Block(user, AdminId, "  abc  ", Now));
        Assert.Throws<DomainException>(() => UserBlocking.Block(user, AdminId, new string('x', UserBlocking.ReasonMax + 1), Now));
        Assert.False(user.IsBlocked);

        var entry = UserBlocking.Block(user, AdminId, "  Abusive messages  ", Now);
        Assert.True(user.IsBlocked);
        Assert.Equal(Now, user.BlockedAt);
        Assert.Equal("Abusive messages", user.BlockedReason);
        Assert.Equal(user.Id, entry.UserId);
        Assert.Equal(AdminId, entry.AdminUserId);
        Assert.Equal(UserBlockAction.Blocked, entry.Action);
        Assert.Equal("Abusive messages", entry.Reason);
        Assert.Equal(Now, entry.CreatedAt);

        var again = Assert.Throws<DomainException>(() => UserBlocking.Block(user, AdminId, "Second try", Now));
        Assert.Equal(409, again.StatusCode);
    }

    [Fact]
    public void Admins_cannot_be_blocked()
    {
        var admin = new User { Id = Guid.NewGuid(), Role = UserRole.Admin };
        var error = Assert.Throws<DomainException>(() => UserBlocking.Block(admin, AdminId, "Not allowed", Now));
        Assert.Equal(400, error.StatusCode);
        Assert.False(admin.IsBlocked);
    }

    [Fact]
    public void Unblock_clears_the_block_with_an_optional_reason()
    {
        var user = new User { Id = Guid.NewGuid(), Role = UserRole.Provider };
        var notBlocked = Assert.Throws<DomainException>(() => UserBlocking.Unblock(user, AdminId, null, Now));
        Assert.Equal(409, notBlocked.StatusCode);

        UserBlocking.Block(user, AdminId, "Fake listing", Now);
        Assert.Throws<DomainException>(() => UserBlocking.Unblock(user, AdminId, new string('x', UserBlocking.ReasonMax + 1), Now));
        Assert.True(user.IsBlocked);

        var entry = UserBlocking.Unblock(user, AdminId, "   ", Now.AddHours(1));
        Assert.False(user.IsBlocked);
        Assert.Null(user.BlockedAt);
        Assert.Null(user.BlockedReason);
        Assert.Equal(UserBlockAction.Unblocked, entry.Action);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public void Blocked_users_cannot_sign_in_and_others_can()
    {
        UserBlocking.EnsureCanSignIn(null);
        UserBlocking.EnsureCanSignIn(new User { IsBlocked = false });
        var error = Assert.Throws<DomainException>(() => UserBlocking.EnsureCanSignIn(new User { IsBlocked = true }));
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void Listing_needs_a_verified_provider_whose_user_is_not_blocked()
    {
        var listed = ProviderApproval.IsListed.Compile();
        var provider = new Provider { Status = ProviderStatus.Verified, User = new User() };
        Assert.True(listed(provider));
        Assert.True(ProviderApproval.IsListedNow(provider));

        provider.User.IsBlocked = true;
        Assert.False(listed(provider));
        Assert.False(ProviderApproval.IsListedNow(provider));

        var pending = new Provider { Status = ProviderStatus.Pending, User = new User() };
        Assert.False(ProviderApproval.IsListedNow(pending));
        Assert.Throws<InvalidOperationException>(() => ProviderApproval.IsListedNow(new Provider { Status = ProviderStatus.Verified }));
    }
}

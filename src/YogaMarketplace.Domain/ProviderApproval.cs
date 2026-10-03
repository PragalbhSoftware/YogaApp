using System.Linq.Expressions;

namespace YogaMarketplace.Domain;

/// <summary>
/// Admin verify and reject. Public browse only lists <see cref="ProviderStatus.Verified"/> providers
/// whose user is not blocked. Existing bookings are left as they are.
/// </summary>
public static class ProviderApproval
{
    public const int MaxReasonLength = 300;

    /// <summary>Listed on public browse and bookable. Needs <see cref="Provider.User"/> in the query.</summary>
    public static readonly Expression<Func<Provider, bool>> IsListed =
        p => p.Status == ProviderStatus.Verified && !p.User!.IsBlocked;

    /// <summary>Same rule as <see cref="IsListed"/> for a loaded provider. <see cref="Provider.User"/> must be loaded.</summary>
    public static bool IsListedNow(Provider provider)
    {
        var user = provider.User ?? throw new InvalidOperationException("Provider user was not loaded.");
        return provider.Status == ProviderStatus.Verified && !user.IsBlocked;
    }

    public static void Verify(Provider provider)
    {
        if (provider.Status == ProviderStatus.Verified)
            return;

        provider.Status = ProviderStatus.Verified;
        provider.RejectionReason = null;
        provider.ReviewedAt = DateTimeOffset.UtcNow;
    }

    public static void Reject(Provider provider, string? reason)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > MaxReasonLength })
            throw new DomainException($"Rejection reason must be {MaxReasonLength} characters or less.");

        if (provider.Status == ProviderStatus.Rejected && provider.RejectionReason == trimmed)
            return;

        provider.Status = ProviderStatus.Rejected;
        provider.RejectionReason = trimmed;
        provider.ReviewedAt = DateTimeOffset.UtcNow;
    }
}

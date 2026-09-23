namespace YogaMarketplace.Domain;

/// <summary>
/// Admin verify and reject. Public browse only lists <see cref="ProviderStatus.Verified"/>.
/// Existing bookings are left as they are.
/// </summary>
public static class ProviderApproval
{
    public const int MaxReasonLength = 300;

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

namespace YogaMarketplace.Domain;

public enum ConsentPurpose
{
    Terms,
    Privacy
}

/// <summary>One user's acceptance of one policy version. The client IP is kept only as a peppered hash.</summary>
public class ConsentRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ConsentPurpose Purpose { get; set; }
    public int PolicyVersion { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public string? IpHash { get; set; }
}

public static class ConsentRules
{
    public const int IpHashLength = 64;
    public static readonly IReadOnlyList<ConsentPurpose> Purposes = [ConsentPurpose.Terms, ConsentPurpose.Privacy];

    /// <summary>True when any purpose has no acceptance at <paramref name="currentVersion"/> or later.</summary>
    public static bool IsRequired(IEnumerable<ConsentRecord> records, int currentVersion)
    {
        var accepted = records
            .GroupBy(r => r.Purpose)
            .ToDictionary(g => g.Key, g => g.Max(r => r.PolicyVersion));
        return Purposes.Any(p => !accepted.TryGetValue(p, out var version) || version < currentVersion);
    }
}

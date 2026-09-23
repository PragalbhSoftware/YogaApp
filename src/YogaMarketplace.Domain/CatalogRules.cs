namespace YogaMarketplace.Domain;

/// <summary>
/// Masters edits for the Mumbai launch. Category slug stays put so public browse and the web shell keep using <c>yoga</c>.
/// Policy changes apply to future payouts. Rows already in <see cref="PayoutPending"/> keep the fee captured at completion.
/// </summary>
public static class CatalogRules
{
    public const string LaunchCity = "Mumbai";
    public const int MaxWindowHours = 168;

    public static Area CreateArea(string? city, string? name) => new()
    {
        Id = Guid.NewGuid(),
        City = NormalizeCity(city),
        Name = NormalizeAreaName(name),
        IsActive = true
    };

    public static void RenameArea(Area area, string? name) =>
        area.Name = NormalizeAreaName(name);

    public static void RenameCategory(Category category, string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length is < 2 or > 80)
            throw new DomainException("Category name must be 2 to 80 characters.");
        category.Name = trimmed;
    }

    public static void UpdatePolicy(
        MarketplacePolicy policy,
        decimal? platformFeePercent,
        int? cancelFreeWindowHours,
        int? rescheduleFreeWindowHours,
        decimal? lateCancelFeePercent,
        string? policyNote)
    {
        if (platformFeePercent is null
            && cancelFreeWindowHours is null
            && rescheduleFreeWindowHours is null
            && lateCancelFeePercent is null
            && policyNote is null)
        {
            throw new DomainException("Send at least one policy field.");
        }

        if (platformFeePercent is decimal fee)
            policy.PlatformFeePercent = Percent(fee, "Platform fee percent");
        if (cancelFreeWindowHours is int cancelHours)
            policy.CancelFreeWindowHours = Hours(cancelHours, "Cancel free window");
        if (rescheduleFreeWindowHours is int rescheduleHours)
            policy.RescheduleFreeWindowHours = Hours(rescheduleHours, "Reschedule free window");
        if (lateCancelFeePercent is decimal lateFee)
            policy.LateCancelFeePercent = Percent(lateFee, "Late cancel fee percent");
        if (policyNote is not null)
        {
            var note = policyNote.Trim();
            if (note.Length > 400)
                throw new DomainException("Policy note must be 400 characters or less.");
            policy.PolicyNote = note;
        }
    }

    private static string NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return LaunchCity;
        if (!city.Trim().Equals(LaunchCity, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"{LaunchCity} is the only city in this release.");
        return LaunchCity;
    }

    private static string NormalizeAreaName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length is < 2 or > 80)
            throw new DomainException("Area name must be 2 to 80 characters.");
        return trimmed;
    }

    private static decimal Percent(decimal value, string label)
    {
        if (value is < 0 or > 100)
            throw new DomainException($"{label} must be between 0 and 100.");
        if (decimal.Round(value, 2) != value)
            throw new DomainException($"{label} supports at most 2 decimal places.");
        return value;
    }

    private static int Hours(int value, string label)
    {
        if (value is < 0 or > MaxWindowHours)
            throw new DomainException($"{label} must be between 0 and {MaxWindowHours} hours.");
        return value;
    }
}

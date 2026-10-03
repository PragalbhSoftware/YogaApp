using System.Globalization;

namespace YogaMarketplace.Domain;

/// <summary>
/// Masters edits. Cities are data: every new area names its city, and the owner opens a city by adding its first area.
/// Category slug stays put so public browse keeps using the slug it was created with.
/// Policy changes apply to future payouts. Rows already in <see cref="PayoutPending"/> keep the fee captured at completion.
/// </summary>
public static class CatalogRules
{
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

    public const int CityMin = 2;
    /// <summary>Matches the <c>Areas.City</c> column length.</summary>
    public const int CityMax = 40;

    public static string NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City is required.");
        var collapsed = string.Join(' ', city.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (collapsed.Length is < CityMin or > CityMax)
            throw new DomainException($"City must be {CityMin} to {CityMax} characters.");
        if (!collapsed.All(c => char.IsLetter(c) || c is ' ' or '-' or '.'))
            throw new DomainException("City can use letters, spaces, hyphens, and dots only.");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
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

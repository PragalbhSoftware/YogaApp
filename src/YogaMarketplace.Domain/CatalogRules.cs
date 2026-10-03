using System.Globalization;

namespace YogaMarketplace.Domain;

/// <summary>
/// Masters edits. Cities are data: every area names its city. New areas go into cities that already exist.
/// Turning an area off hides it from browse; bookings already made there are untouched.
/// Category slug stays put so public browse keeps using the slug it was created with.
/// </summary>
public static class CatalogRules
{
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
}

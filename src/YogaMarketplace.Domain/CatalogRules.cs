using System.Globalization;

namespace YogaMarketplace.Domain;

/// <summary>
/// Masters edits. A city belongs to a state. A neighbourhood names an active city of an active state.
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

    public static State CreateState(string? name, string? code, int displayOrder) => new()
    {
        Id = Guid.NewGuid(),
        Name = NormalizeStateName(name),
        Code = NormalizeStateCode(code),
        IsActive = true,
        DisplayOrder = displayOrder
    };

    public static City CreateCity(string? name, Guid stateId, int displayOrder)
    {
        var normalized = NormalizeCity(name);
        return new City
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            StateId = stateId,
            IsActive = true,
            DisplayOrder = displayOrder,
            Slug = CitySlug(normalized)
        };
    }

    public static void RenameCity(City city, string? name)
    {
        city.Name = NormalizeCity(name);
        city.Slug = CitySlug(city.Name);
    }

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
    public const int StateNameMax = 80;
    public const int StateCodeMax = 10;
    public const int SlugMax = CityMax;

    public static string NormalizeCity(string? city) => NormalizePlaceName(city, CityMax, "City");

    public static string NormalizeStateName(string? name) => NormalizePlaceName(name, StateNameMax, "State");

    public static string NormalizeStateCode(string? code)
    {
        var trimmed = (code ?? "").Trim();
        if (trimmed.Length is < CityMin or > StateCodeMax || !trimmed.All(char.IsLetter))
            throw new DomainException($"State code must be {CityMin} to {StateCodeMax} letters.");
        return trimmed.ToUpperInvariant();
    }

    /// <summary>Kebab-case of a name already passed through <see cref="NormalizeCity"/>.</summary>
    public static string CitySlug(string normalizedName)
    {
        var slug = normalizedName.ToLowerInvariant().Replace(".", "", StringComparison.Ordinal).Replace(' ', '-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }

    private static string NormalizePlaceName(string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{label} is required.");
        var collapsed = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (collapsed.Length < CityMin || collapsed.Length > max)
            throw new DomainException($"{label} must be {CityMin} to {max} characters.");
        if (!collapsed.All(c => char.IsLetter(c) || c is ' ' or '-' or '.'))
            throw new DomainException($"{label} can use letters, spaces, hyphens, and dots only.");
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

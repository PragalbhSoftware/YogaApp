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

    public static City CreateCity(string? name, Guid stateId, string? stateCode, int displayOrder)
    {
        var normalized = NormalizeCityName(name);
        return new City
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            StateId = stateId,
            IsActive = true,
            DisplayOrder = displayOrder,
            Slug = CitySlug(stateCode, normalized)
        };
    }

    public static void RenameCity(City city, string? name, string? stateCode)
    {
        city.Name = NormalizeCityName(name);
        city.Slug = CitySlug(stateCode, city.Name);
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
    /// <summary>City master names. Longer than an area's city text because official district names run past 40.</summary>
    public const int CityNameMax = 80;
    public const int StateNameMax = 80;
    public const int StateCodeMax = 10;
    public const int SlugMax = StateCodeMax + 1 + CityNameMax;

    public static string NormalizeCity(string? city) => NormalizePlaceName(city, CityMax, "City", official: false);

    /// <summary>City master name. Allows the digits and parentheses used in official district names.</summary>
    public static string NormalizeCityName(string? name) => NormalizePlaceName(name, CityNameMax, "City", official: true);

    public static string NormalizeStateName(string? name) => NormalizePlaceName(name, StateNameMax, "State", official: false);

    public static string NormalizeStateCode(string? code)
    {
        var trimmed = (code ?? "").Trim();
        if (trimmed.Length is < CityMin or > StateCodeMax || !trimmed.All(char.IsLetter))
            throw new DomainException($"State code must be {CityMin} to {StateCodeMax} letters.");
        return trimmed.ToUpperInvariant();
    }

    /// <summary>State code plus the city name, so the same name can exist in two states.</summary>
    public static string CitySlug(string? stateCode, string normalizedName)
    {
        var code = NormalizeStateCode(stateCode).ToLowerInvariant();
        var body = string.Join('-', normalizedName
            .ToLowerInvariant()
            .Replace(".", " ", StringComparison.Ordinal)
            .Replace("(", " ", StringComparison.Ordinal)
            .Replace(")", " ", StringComparison.Ordinal)
            .Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries));
        var slug = $"{code}-{body}";
        return slug;
    }

    private static string NormalizePlaceName(string? value, int max, string label, bool official)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{label} is required.");
        var collapsed = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (collapsed.Length < CityMin || collapsed.Length > max)
            throw new DomainException($"{label} must be {CityMin} to {max} characters.");
        var allowed = collapsed.All(c => char.IsLetter(c) || c is ' ' or '-' or '.' || (official && (char.IsDigit(c) || c is '(' or ')')));
        if (!allowed)
            throw new DomainException(official
                ? $"{label} can use letters, digits, spaces, hyphens, dots, and parentheses only."
                : $"{label} can use letters, spaces, hyphens, and dots only.");
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

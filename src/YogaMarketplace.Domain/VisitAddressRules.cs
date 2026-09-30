namespace YogaMarketplace.Domain;

/// <summary>
/// One default Home-visit address on the customer. Bookings still snapshot address and landmark.
/// </summary>
public static class VisitAddressRules
{
    public const int Line1Min = 4;
    public const int Line1Max = 160;
    public const int AreaMin = 2;
    public const int AreaMax = 80;
    public const int CityMin = 2;
    public const int CityMax = 60;
    public const int LandmarkMin = 2;
    public const int LandmarkMax = 160;
    public const int HomeAddressMax = 300;

    public static bool IsComplete(User user) =>
        HasText(user.HomeLine1)
        && HasText(user.HomeArea)
        && HasText(user.HomeCity)
        && HasText(user.HomePin)
        && HasText(user.HomeLandmark);

    public static string Compose(string line1, string area, string city, string pin) =>
        $"{line1.Trim()}, {area.Trim()}, {city.Trim()} {pin.Trim()}";

    public static void Set(
        User user,
        string? line1,
        string? area,
        string? city,
        string? pin,
        string? landmark)
    {
        var cleanLine1 = Require(line1, Line1Min, Line1Max, "Enter house and street.");
        var cleanArea = Require(area, AreaMin, AreaMax, "Enter the area or locality.");
        var cleanCity = Require(city, CityMin, CityMax, "Enter the city.");
        var cleanPin = RequirePin(pin);
        var cleanLandmark = Require(landmark, LandmarkMin, LandmarkMax, "Enter a landmark.");
        var composed = Compose(cleanLine1, cleanArea, cleanCity, cleanPin);
        if (composed.Length > HomeAddressMax)
            throw new DomainException("Address is too long.");

        user.HomeLine1 = cleanLine1;
        user.HomeArea = cleanArea;
        user.HomeCity = cleanCity;
        user.HomePin = cleanPin;
        user.HomeLandmark = cleanLandmark;
    }

    public static (string HomeAddress, string Landmark) ForBooking(User user)
    {
        if (!IsComplete(user))
            throw new DomainException("Home sessions require an address and a landmark.");

        return (Compose(user.HomeLine1!, user.HomeArea!, user.HomeCity!, user.HomePin!), user.HomeLandmark!.Trim());
    }

    private static string Require(string? value, int min, int max, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(emptyMessage);

        var trimmed = value.Trim();
        if (trimmed.Length < min)
            throw new DomainException(emptyMessage);
        if (trimmed.Length > max)
            throw new DomainException($"Must be {max} characters or less.");
        return trimmed;
    }

    private static string RequirePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new DomainException("Enter a 6-digit PIN code.");

        var digits = pin.Trim();
        if (digits.Length != 6 || digits[0] == '0' || !digits.All(char.IsDigit))
            throw new DomainException("Enter a 6-digit PIN code.");
        return digits;
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

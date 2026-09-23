namespace YogaMarketplace.Domain;

public static class PhoneNumber
{
    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = "";
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Phone is required.";
            return false;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
            digits = "91" + digits;

        if (digits.Length != 12 || !digits.StartsWith("91", StringComparison.Ordinal) || digits[2] is < '6' or > '9')
        {
            error = "Enter a valid Indian mobile number.";
            return false;
        }

        normalized = "+" + digits;
        return true;
    }
}

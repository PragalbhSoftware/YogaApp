using System.Text.RegularExpressions;

namespace YogaMarketplace.Api.Security;

/// <summary>
/// Masks personal data before it reaches a log, an error row or an alert. Never log OTP codes, secrets or card
/// data at all; this is the safety net for values that slip into free text such as exception messages.
/// </summary>
public static partial class LogMask
{
    private const int VisibleDigits = 4;

    /// <summary>Keeps the last 4 digits: <c>+919876543210</c> becomes <c>*********3210</c>.</summary>
    public static string Phone(string phone) =>
        phone.Length <= VisibleDigits ? new string('*', phone.Length) : new string('*', phone.Length - VisibleDigits) + phone[^VisibleDigits..];

    /// <summary>
    /// Masks sensitive values in <paramref name="text"/>: bearer tokens and JWTs are replaced, email addresses keep
    /// only their domain, and phone-like or card-like numbers keep their last 4 digits (a run of 10 to 19 digits,
    /// optionally starting with +, or 4-digit groups split by spaces or dashes). Ids such as GUIDs are left alone.
    /// </summary>
    // ponytail: pattern-based, so a phone written with irregular spacing slips through; mask at the call site first.
    public static string Text(string text)
    {
        var masked = BearerToken().Replace(text, "Bearer [token]");
        masked = Jwt().Replace(masked, "[jwt]");
        masked = Email().Replace(masked, "***$1");
        return SensitiveNumber().Replace(masked, match => Phone(new string(match.Value.Where(char.IsAsciiDigit).ToArray())));
    }

    [GeneratedRegex(@"\bBearer\s+[\w.~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"\beyJ[\w-]+\.[\w-]+\.[\w-]*")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?<![\w.%+-])[\w.%+-]+(@(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,})")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<![\w-])(?:\+?\d{10,19}|\d{4}(?:[ -]\d{4}){2,3}(?:[ -]\d{1,4})?)(?![\w-])")]
    private static partial Regex SensitiveNumber();
}

using System.Text.RegularExpressions;

namespace YogaMarketplace.Api.Security;

/// <summary>
/// Masks personal numbers before they reach a log, an error row or an alert. Never log OTP codes, secrets or card
/// data at all; this is the safety net for numbers that slip into free text such as exception messages.
/// </summary>
public static partial class LogMask
{
    private const int VisibleDigits = 4;

    /// <summary>Keeps the last 4 digits: <c>+919876543210</c> becomes <c>*********3210</c>.</summary>
    public static string Phone(string phone) =>
        phone.Length <= VisibleDigits ? new string('*', phone.Length) : new string('*', phone.Length - VisibleDigits) + phone[^VisibleDigits..];

    /// <summary>
    /// Masks every phone-like or card-like number in <paramref name="text"/> to its last 4 digits: a run of 10 to 19
    /// digits (optionally starting with +), or 4-digit groups split by spaces or dashes. Ids such as GUIDs are left alone.
    /// </summary>
    // ponytail: pattern-based, so a phone written with irregular spacing slips through; mask at the call site first.
    public static string Text(string text) => SensitiveNumber().Replace(text, match =>
    {
        var digits = new string(match.Value.Where(char.IsAsciiDigit).ToArray());
        return Phone(digits);
    });

    [GeneratedRegex(@"(?<![\w-])(?:\+?\d{10,19}|\d{4}(?:[ -]\d{4}){2,3}(?:[ -]\d{1,4})?)(?![\w-])")]
    private static partial Regex SensitiveNumber();
}

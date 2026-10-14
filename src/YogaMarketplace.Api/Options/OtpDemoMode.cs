using YogaMarketplace.Api.Security;
using YogaMarketplace.Api.Services;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Options;

/// <summary>
/// Demo site sign-in: phones in Otp:DemoPhones get Otp:DemoCode instead of a random code. Everything else
/// (expiry, attempt lockout, rate limits, blocking) is the normal OTP flow. Any other phone gets a normal code.
/// </summary>
public sealed class OtpDemoMode
{
    public const int CodeLength = 6;

    private readonly HashSet<string> _phones;

    private OtpDemoMode(HashSet<string> phones, string code, bool hasInvalidPhone)
    {
        _phones = phones;
        Code = code;
        HasInvalidPhone = hasInvalidPhone;
    }

    public string Code { get; }
    public bool HasInvalidPhone { get; }
    public bool IsOn => _phones.Count > 0 && Code.Length > 0;
    public bool IsConfigured => _phones.Count > 0 || Code.Length > 0 || HasInvalidPhone;
    public IReadOnlyCollection<string> Phones => _phones;

    public static OtpDemoMode From(OtpOptions options)
    {
        var phones = new HashSet<string>(StringComparer.Ordinal);
        var hasInvalid = false;
        foreach (var entry in (options.DemoPhones ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (PhoneNumber.TryNormalize(entry, out var phone, out _))
                phones.Add(phone);
            else
                hasInvalid = true;
        }
        return new OtpDemoMode(phones, (options.DemoCode ?? "").Trim(), hasInvalid);
    }

    /// <summary>The demo code for a listed phone; null for every other phone or when demo mode is off.</summary>
    public string? CodeFor(string normalizedPhone) => IsOn && _phones.Contains(normalizedPhone) ? Code : null;

    public string? StartupWarning() => IsOn
        ? $"OTP demo mode is on for {string.Join(", ", _phones.Select(LogMask.Phone))}. These phones sign in with the demo code."
        : null;

    public IEnumerable<string> ProductionProblems()
    {
        if (Code.Length > 0 && _phones.Count == 0)
            yield return "Otp:DemoCode is set without Otp:DemoPhones. List the demo phones or clear the demo code.";
        if (Code.Length > 0 && (Code.Length != CodeLength || !Code.All(char.IsAsciiDigit)))
            yield return $"Otp:DemoCode must be {CodeLength} digits.";
        if (HasInvalidPhone)
            yield return "Otp:DemoPhones has an entry that is not a valid phone number.";
    }
}

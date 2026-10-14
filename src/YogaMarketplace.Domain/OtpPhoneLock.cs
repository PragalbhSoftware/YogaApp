namespace YogaMarketplace.Domain;

/// <summary>A phone that may not request or verify sign-in codes until <see cref="LockedUntil"/>.</summary>
public class OtpPhoneLock
{
    public string Phone { get; set; } = "";
    public DateTimeOffset LockedUntil { get; set; }
}

namespace YogaMarketplace.Domain;

/// <summary>
/// Per-phone sign-in code limits. Sends and wrong codes are counted over the last <see cref="LockMinutes"/>;
/// reaching either limit locks the phone for <see cref="LockMinutes"/>.
/// </summary>
public sealed record OtpLimitTerms(int MaxSends, int MaxWrongCodes, int LockMinutes);

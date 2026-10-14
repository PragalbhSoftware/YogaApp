namespace YogaMarketplace.Domain;

/// <summary>Whether notification emails go out, and how many times one send is tried before it fails for good.</summary>
public sealed record NotificationTerms(bool EmailEnabled, int MaxAttempts);

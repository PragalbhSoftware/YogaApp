namespace YogaMarketplace.Api.Services;

/// <summary>Who a notification is for. A null <see cref="Email"/> queues a row that is Skipped with "no email on file".</summary>
public sealed record NotificationRecipient(Guid? UserId, string? Email, string? Phone);

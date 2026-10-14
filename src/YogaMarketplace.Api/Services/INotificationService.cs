namespace YogaMarketplace.Api.Services;

public interface INotificationService
{
    /// <summary>
    /// Adds an outbox row and its send job to the context without saving, so they commit or roll back with the caller's
    /// next <c>SaveChanges</c>. <paramref name="categoryId"/> picks a category template over the default.
    /// </summary>
    Task EnqueueAsync(
        string eventKey,
        NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> data,
        Guid? categoryId,
        CancellationToken cancellationToken);
}

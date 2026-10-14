namespace YogaMarketplace.Domain;

/// <summary>Skipped means the send was not attempted on purpose (feature off, no SMTP, no address), so it is not an error.</summary>
public enum NotificationStatus
{
    Pending,
    Sent,
    Skipped,
    Failed
}

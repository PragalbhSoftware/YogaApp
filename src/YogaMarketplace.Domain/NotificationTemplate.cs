namespace YogaMarketplace.Domain;

/// <summary>
/// Text for one event on one channel. A template with a <see cref="CategoryId"/> overrides the default (no category)
/// for bookings in that category. Subject and body use <c>{{key}}</c> placeholders.
/// </summary>
public class NotificationTemplate
{
    public const int EventKeyMax = 64;
    public const int SubjectMax = 200;
    public const int BodyMax = 4000;
    public const int DltTemplateIdMax = 32;

    public Guid Id { get; set; }
    public string EventKey { get; set; } = "";
    public NotificationChannel Channel { get; set; }
    public Guid? CategoryId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    /// <summary>The DLT-registered template id an SMS must carry in India. Unused for email.</summary>
    public string? DltTemplateId { get; set; }

    public bool IsActive { get; set; } = true;
}

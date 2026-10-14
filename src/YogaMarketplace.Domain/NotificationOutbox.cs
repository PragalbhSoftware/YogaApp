namespace YogaMarketplace.Domain;

/// <summary>
/// One notification to send. Saved with the caller's work, so a rolled-back booking leaves no row; a background job
/// sends it. <see cref="Data"/> is a JSON object of placeholder values.
/// </summary>
public class NotificationOutbox
{
    public const int RecipientMax = 254;
    public const int DataMax = 4000;
    public const int LastErrorMax = 1000;

    public Guid Id { get; set; }
    public string EventKey { get; set; } = "";
    public NotificationChannel Channel { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>The address on <see cref="Channel"/>, or null when the user has none (the row is then Skipped).</summary>
    public string? Recipient { get; set; }

    public string Data { get; set; } = "{}";
    public NotificationStatus Status { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

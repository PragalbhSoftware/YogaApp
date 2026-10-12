namespace YogaMarketplace.Domain;

/// <summary>An admin-edited public page such as the refund policy. The body is plain text with simple formatting.</summary>
public class ContentPage
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Page bodies use three rules, read by the client: a blank line starts a new paragraph, a line starting with
/// "## " is a heading, and a line starting with "- " is a bullet. Nothing else is interpreted, so no HTML or Markdown.
/// </summary>
public static class ContentPageRules
{
    public const string RefundPolicy = "refund-policy";
    public const string Terms = "terms";
    public const string Privacy = "privacy";

    public static readonly IReadOnlyList<string> Slugs = [RefundPolicy, Terms, Privacy];

    public const int SlugMax = 32;
    public const int TitleMin = 2;
    public const int TitleMax = 120;
    public const int BodyMax = 20_000;

    public const string StaleMessage =
        "Another admin saved this page first. We reloaded the latest text, so your unsaved edits were discarded.";

    public static bool IsKnown(string? slug) => slug is not null && Slugs.Contains(slug);

    /// <summary>Applies an edit and bumps the version. Returns false when nothing changed.</summary>
    public static bool Apply(ContentPage page, string? title, string? body, Guid adminUserId, DateTimeOffset now)
    {
        var cleanTitle = (title ?? "").Trim();
        if (cleanTitle.Length is < TitleMin or > TitleMax)
            throw new DomainException($"Title must be {TitleMin} to {TitleMax} characters.");

        var cleanBody = (body ?? "").Replace("\r\n", "\n").Trim();
        if (cleanBody.Length == 0)
            throw new DomainException("The page needs some text.");
        if (cleanBody.Length > BodyMax)
            throw new DomainException($"Keep the page under {BodyMax:N0} characters.");

        if (cleanTitle == page.Title && cleanBody == page.Body)
            return false;

        page.Title = cleanTitle;
        page.Body = cleanBody;
        page.Version += 1;
        page.UpdatedByUserId = adminUserId;
        page.UpdatedAt = now;
        return true;
    }
}

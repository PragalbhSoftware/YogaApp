using System.Text.RegularExpressions;

namespace YogaMarketplace.Domain;

/// <summary>A public profile photo. The file lives in file storage under <see cref="StorageKey"/>.</summary>
public class ProviderPhoto
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A certificate file. Private for good: only the instructor and admins can open the file.
/// The public profile shows the titles of approved certificates and the Verified badge.
/// </summary>
public class ProviderCertificate
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public string Title { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Pending;
    public string? RejectionReason { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Limits and checks for instructor photos, certificates and the intro video.
/// File types come from the file's first bytes, never from its name or the browser's content type.
/// An instructor is Verified once at least one certificate is approved.
/// </summary>
public static partial class ProviderMediaRules
{
    public const int MaxPhotos = 6;
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const int MaxCertificates = 5;
    public const long MaxCertificateBytes = 10 * 1024 * 1024;
    public const int CertificateTitleMin = 2;
    public const int CertificateTitleMax = 120;
    public const int ReasonMax = 300;
    public const int YouTubeIdLength = 11;

    /// <summary>Bytes needed to recognise every supported type.</summary>
    public const int HeaderBytes = 12;

    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";

    private static readonly string[] PhotoTypes = [Jpeg, Png, Webp];
    private static readonly string[] CertificateTypes = [Pdf, Jpeg, Png];

    private static readonly string[] YouTubeHosts =
        ["youtube.com", "www.youtube.com", "m.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com"];

    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return Jpeg;
        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return Png;
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return Webp;
        if (header.StartsWith("%PDF-"u8))
            return Pdf;
        return null;
    }

    public static string ExtensionFor(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        Webp => ".webp",
        Pdf => ".pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported content type.")
    };

    /// <summary>Returns the photo's content type, or throws if it is the wrong type, too large, or one too many.</summary>
    public static string EnsurePhoto(ReadOnlySpan<byte> header, long sizeBytes, int existingPhotos)
    {
        if (existingPhotos >= MaxPhotos)
            throw new DomainException($"You can add up to {MaxPhotos} photos. Remove one first.");
        return EnsureFile(header, sizeBytes, PhotoTypes, MaxPhotoBytes, "Photos must be JPEG, PNG or WebP.");
    }

    public static string EnsureCertificate(ReadOnlySpan<byte> header, long sizeBytes, int existingCertificates)
    {
        if (existingCertificates >= MaxCertificates)
            throw new DomainException($"You can upload up to {MaxCertificates} certificates. Remove one first.");
        return EnsureFile(header, sizeBytes, CertificateTypes, MaxCertificateBytes, "Certificates must be PDF, JPEG or PNG.");
    }

    public static string CleanCertificateTitle(string? title)
    {
        var trimmed = (title ?? "").Trim();
        if (trimmed.Length is < CertificateTitleMin or > CertificateTitleMax)
            throw new DomainException($"Certificate title must be {CertificateTitleMin} to {CertificateTitleMax} characters.");
        return trimmed;
    }

    /// <summary>
    /// Reads a YouTube watch, share, shorts, live or embed link and returns the 11-character video id.
    /// An empty value returns null, which clears the video.
    /// </summary>
    public static string? ParseYouTubeId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new DomainException(InvalidVideo);

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? id = null;
        if (host == "youtu.be")
            id = segments.FirstOrDefault();
        else if (YouTubeHosts.Contains(host))
        {
            if (segments is ["watch"])
                id = QueryValue(uri.Query, "v");
            else if (segments is [("embed" or "shorts" or "live"), var value, ..])
                id = value;
        }

        if (id is null || !YouTubeId().IsMatch(id))
            throw new DomainException(InvalidVideo);
        return id;
    }

    public static void Approve(ProviderCertificate certificate, Guid adminUserId, DateTimeOffset now)
    {
        certificate.Status = CertificateStatus.Approved;
        certificate.RejectionReason = null;
        certificate.ReviewedByUserId = adminUserId;
        certificate.ReviewedAt = now;
    }

    public static void Reject(ProviderCertificate certificate, string? reason, Guid adminUserId, DateTimeOffset now)
    {
        var trimmed = (reason ?? "").Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Give a reason so the instructor knows what to fix.");
        if (trimmed.Length > ReasonMax)
            throw new DomainException($"Reason must be {ReasonMax} characters or less.");
        certificate.Status = CertificateStatus.Rejected;
        certificate.RejectionReason = trimmed;
        certificate.ReviewedByUserId = adminUserId;
        certificate.ReviewedAt = now;
    }

    public static bool IsVerified(IEnumerable<ProviderCertificate> certificates) =>
        certificates.Any(c => c.Status == CertificateStatus.Approved);

    private const string InvalidVideo = "Paste a YouTube link, for example https://www.youtube.com/watch?v=… or https://youtu.be/….";

    private static string EnsureFile(ReadOnlySpan<byte> header, long sizeBytes, string[] allowed, long maxBytes, string typeMessage)
    {
        if (sizeBytes <= 0)
            throw new DomainException("The file is empty.");
        if (sizeBytes > maxBytes)
            throw new DomainException($"The file must be {maxBytes / (1024 * 1024)} MB or smaller.");
        var type = DetectContentType(header);
        if (type is null || !allowed.Contains(type))
            throw new DomainException(typeMessage);
        return type;
    }

    private static string? QueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key)
                return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();
}

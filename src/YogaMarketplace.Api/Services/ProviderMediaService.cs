using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;
using YogaMarketplace.Infrastructure.Storage;

namespace YogaMarketplace.Api.Services;

public sealed record StoredFile(Stream Content, string ContentType, string FileName);

/// <summary>
/// Instructor photos, intro video and certificates. Files are saved to storage before the row is saved,
/// and removed again if the save fails, so a row never points at a missing file.
/// </summary>
public class ProviderMediaService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IFileStorage _storage;
    private readonly ILogger<ProviderMediaService> _logger;
    private readonly TimeProvider _time;

    public ProviderMediaService(YogaDbContext db, ICurrentUser current, IFileStorage storage, ILogger<ProviderMediaService> logger, TimeProvider time)
    {
        _db = db;
        _current = current;
        _storage = storage;
        _logger = logger;
        _time = time;
    }

    public async Task<ProviderMediaResponse> GetMineAsync(CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        return await MediaOfAsync(provider, cancellationToken);
    }

    public async Task<ProviderPhotoResponse> AddPhotoAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var upload = RequireFile(file);
        var existing = await _db.ProviderPhotos.Where(p => p.ProviderId == provider.Id).ToListAsync(cancellationToken);
        var contentType = ProviderMediaRules.EnsurePhoto(await ReadHeaderAsync(upload, cancellationToken), upload.Length, existing.Count);

        var photo = new ProviderPhoto
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ContentType = contentType,
            SizeBytes = upload.Length,
            SortOrder = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1,
            CreatedAt = _time.GetUtcNow()
        };
        photo.StorageKey = $"photos/{provider.Id:N}/{photo.Id:N}{ProviderMediaRules.ExtensionFor(contentType)}";
        await SaveWithFileAsync(photo.StorageKey, upload, () => _db.ProviderPhotos.Add(photo), cancellationToken);
        return ToPhoto(photo);
    }

    public async Task DeletePhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var photo = await _db.ProviderPhotos.SingleOrDefaultAsync(p => p.Id == photoId && p.ProviderId == provider.Id, cancellationToken)
            ?? throw new DomainException("Photo not found.", 404);
        _db.ProviderPhotos.Remove(photo);
        await _db.SaveChangesAsync(cancellationToken);
        await DeleteFileAsync(photo.StorageKey, cancellationToken);
    }

    public async Task<ProviderMediaResponse> SetIntroVideoAsync(string? url, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken, tracking: true);
        provider.IntroVideoId = ProviderMediaRules.ParseYouTubeId(url);
        await _db.SaveChangesAsync(cancellationToken);
        return await MediaOfAsync(provider, cancellationToken);
    }

    public async Task<ProviderCertificateResponse> AddCertificateAsync(string? title, IFormFile? file, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var cleanTitle = ProviderMediaRules.CleanCertificateTitle(title);
        var upload = RequireFile(file);
        var count = await _db.ProviderCertificates.CountAsync(c => c.ProviderId == provider.Id, cancellationToken);
        var contentType = ProviderMediaRules.EnsureCertificate(await ReadHeaderAsync(upload, cancellationToken), upload.Length, count);

        var certificate = new ProviderCertificate
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            Title = cleanTitle,
            ContentType = contentType,
            SizeBytes = upload.Length,
            Status = CertificateStatus.Pending,
            CreatedAt = _time.GetUtcNow()
        };
        certificate.StorageKey = $"certificates/{provider.Id:N}/{certificate.Id:N}{ProviderMediaRules.ExtensionFor(contentType)}";
        await SaveWithFileAsync(certificate.StorageKey, upload, () => _db.ProviderCertificates.Add(certificate), cancellationToken);
        return ToCertificate(certificate);
    }

    public async Task DeleteCertificateAsync(Guid certificateId, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var certificate = await _db.ProviderCertificates
            .SingleOrDefaultAsync(c => c.Id == certificateId && c.ProviderId == provider.Id, cancellationToken)
            ?? throw new DomainException("Certificate not found.", 404);
        _db.ProviderCertificates.Remove(certificate);
        await _db.SaveChangesAsync(cancellationToken);
        await DeleteFileAsync(certificate.StorageKey, cancellationToken);
    }

    public async Task<StoredFile> OpenMyCertificateAsync(Guid certificateId, CancellationToken cancellationToken)
    {
        var provider = await RequireMineAsync(cancellationToken);
        var certificate = await _db.ProviderCertificates.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == certificateId && c.ProviderId == provider.Id, cancellationToken)
            ?? throw new DomainException("Certificate not found.", 404);
        return await OpenCertificateAsync(certificate, cancellationToken);
    }

    /// <summary>Public photo bytes, only for instructors listed on browse.</summary>
    public async Task<StoredFile> OpenPublicPhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await _db.ProviderPhotos.AsNoTracking()
            .Where(p => p.Id == photoId)
            .Where(p => _db.Providers.Where(ProviderApproval.IsListed).Any(provider => provider.Id == p.ProviderId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Photo not found.", 404);
        var stream = await _storage.OpenReadAsync(photo.StorageKey, cancellationToken)
            ?? throw new DomainException("Photo not found.", 404);
        return new StoredFile(stream, photo.ContentType, $"photo{ProviderMediaRules.ExtensionFor(photo.ContentType)}");
    }

    internal async Task<StoredFile> OpenCertificateAsync(ProviderCertificate certificate, CancellationToken cancellationToken)
    {
        var stream = await _storage.OpenReadAsync(certificate.StorageKey, cancellationToken)
            ?? throw new DomainException("The certificate file is missing. Ask the instructor to upload it again.", 404);
        return new StoredFile(stream, certificate.ContentType, $"certificate{ProviderMediaRules.ExtensionFor(certificate.ContentType)}");
    }

    public static string PhotoUrl(Guid photoId) => $"/api/media/photos/{photoId}";

    public static ProviderPhotoResponse ToPhoto(ProviderPhoto photo) => new(photo.Id, PhotoUrl(photo.Id), photo.SortOrder);

    public static ProviderCertificateResponse ToCertificate(ProviderCertificate certificate) => new(
        certificate.Id,
        certificate.Title,
        certificate.ContentType,
        certificate.SizeBytes,
        certificate.Status.ToString(),
        certificate.RejectionReason,
        certificate.CreatedAt,
        certificate.ReviewedAt);

    private async Task<ProviderMediaResponse> MediaOfAsync(Provider provider, CancellationToken cancellationToken)
    {
        var photos = await _db.ProviderPhotos.AsNoTracking()
            .Where(p => p.ProviderId == provider.Id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);
        var certificates = await _db.ProviderCertificates.AsNoTracking()
            .Where(c => c.ProviderId == provider.Id)
            .ToListAsync(cancellationToken);
        return new ProviderMediaResponse(
            photos.Select(ToPhoto).ToList(),
            provider.IntroVideoId,
            certificates.OrderByDescending(c => c.CreatedAt).Select(ToCertificate).ToList(),
            ProviderMediaRules.IsVerified(certificates));
    }

    private async Task<Provider> RequireMineAsync(CancellationToken cancellationToken, bool tracking = false)
    {
        var query = tracking ? _db.Providers : _db.Providers.AsNoTracking();
        return await query.SingleOrDefaultAsync(p => p.UserId == _current.UserId, cancellationToken)
            ?? throw new DomainException("Only instructors can do that.", 403);
    }

    private async Task SaveWithFileAsync(string key, IFormFile upload, Action addRow, CancellationToken cancellationToken)
    {
        await using (var content = upload.OpenReadStream())
        {
            await _storage.SaveAsync(key, content, cancellationToken);
        }
        try
        {
            addRow();
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteFileAsync(key, CancellationToken.None);
            throw;
        }
    }

    private async Task DeleteFileAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(key, cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StorageKey}.", key);
        }
    }

    private static IFormFile RequireFile(IFormFile? file) =>
        file is { Length: > 0 } ? file : throw new DomainException("Choose a file to upload.");

    private static async Task<byte[]> ReadHeaderAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = new byte[ProviderMediaRules.HeaderBytes];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        return header[..read];
    }
}

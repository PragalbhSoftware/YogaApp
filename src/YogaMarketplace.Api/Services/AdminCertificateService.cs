using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminCertificateService
{
    Task<PagedResponse<AdminCertificateResponse>> ListAsync(string? status, AdminPageQuery? paging, CancellationToken cancellationToken);

    Task<StoredFile> OpenFileAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminCertificateResponse> ApproveAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminCertificateResponse> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken);
}

public class AdminCertificateService : IAdminCertificateService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ProviderMediaService _media;

    public AdminCertificateService(YogaDbContext db, ICurrentUser current, ProviderMediaService media)
    {
        _db = db;
        _current = current;
        _media = media;
    }

    public Task<PagedResponse<AdminCertificateResponse>> ListAsync(
        string? status,
        AdminPageQuery? paging,
        CancellationToken cancellationToken)
    {
        var query = _db.ProviderCertificates.AsNoTracking().Include(c => c.Provider);
        IQueryable<ProviderCertificate> filtered = query;
        if (string.IsNullOrWhiteSpace(status) || status.Trim().Equals("pending", StringComparison.OrdinalIgnoreCase))
            filtered = query.Where(c => c.Status == CertificateStatus.Pending);
        else if (!status.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = AdminQuery.ParseRequired<CertificateStatus>(status, "Unknown certificate status.");
            filtered = query.Where(c => c.Status == parsed);
        }

        var ordered = filtered.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);
        return AdminQuery.PageAsync(ordered, paging, ToResponse, cancellationToken);
    }

    public async Task<StoredFile> OpenFileAsync(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _db.ProviderCertificates.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("Certificate not found.", 404);
        return await _media.OpenCertificateAsync(certificate, cancellationToken);
    }

    public Task<AdminCertificateResponse> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ReviewAsync(id, (certificate, now) => ProviderMediaRules.Approve(certificate, _current.UserId, now), cancellationToken);

    public Task<AdminCertificateResponse> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken) =>
        ReviewAsync(id, (certificate, now) => ProviderMediaRules.Reject(certificate, reason, _current.UserId, now), cancellationToken);

    private async Task<AdminCertificateResponse> ReviewAsync(
        Guid id,
        Action<ProviderCertificate, DateTimeOffset> review,
        CancellationToken cancellationToken)
    {
        var certificate = await _db.ProviderCertificates.Include(c => c.Provider)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException("Certificate not found.", 404);
        review(certificate, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(certificate);
    }

    private static AdminCertificateResponse ToResponse(ProviderCertificate certificate)
    {
        var provider = certificate.Provider ?? throw new InvalidOperationException("Certificate provider was not loaded.");
        return new AdminCertificateResponse(
            certificate.Id,
            provider.Id,
            provider.DisplayName,
            certificate.Title,
            certificate.ContentType,
            certificate.SizeBytes,
            certificate.Status.ToString(),
            certificate.RejectionReason,
            certificate.CreatedAt,
            certificate.ReviewedAt);
    }
}

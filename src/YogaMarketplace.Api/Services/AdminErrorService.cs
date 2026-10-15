using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

/// <summary>Lists logged error types for admin, acknowledges them, and sends a test alert email.</summary>
public class AdminErrorService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAlertSender _sender;
    private readonly ILogger<AdminErrorService> _logger;
    private readonly TimeProvider _time;

    public AdminErrorService(YogaDbContext db, ICurrentUser current, IAlertSender sender, ILogger<AdminErrorService> logger, TimeProvider time)
    {
        _db = db;
        _current = current;
        _sender = sender;
        _logger = logger;
        _time = time;
    }

    public async Task<PagedResponse<AdminErrorEventResponse>> ListAsync(bool acknowledged, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (pageNumber, size) = AdminQuery.Paging(page, pageSize);
        var query = _db.ErrorEvents.AsNoTracking().Where(e => (e.AcknowledgedAt != null) == acknowledged);
        var total = await query.CountAsync(cancellationToken);
        var errors = await query
            .OrderByDescending(e => e.LastSeenAt)
            .ThenBy(e => e.Id)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new PagedResponse<AdminErrorEventResponse>(errors.Select(ToResponse).ToList(), total, pageNumber, size);
    }

    public async Task<AdminErrorSummaryResponse> SummaryAsync(CancellationToken cancellationToken) =>
        new(await _db.ErrorEvents.CountAsync(e => e.AcknowledgedAt == null, cancellationToken));

    /// <summary>Acknowledging twice is fine. The next occurrence of the same type opens it again.</summary>
    public async Task<AdminErrorEventResponse> AcknowledgeAsync(Guid id, CancellationToken cancellationToken)
    {
        DateTimeOffset? now = _time.GetUtcNow();
        Guid? adminId = _current.UserId;
        await _db.ErrorEvents
            .Where(e => e.Id == id && e.AcknowledgedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.AcknowledgedAt, now)
                    .SetProperty(e => e.AcknowledgedByUserId, adminId),
                cancellationToken);
        var error = await _db.ErrorEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Error not found.", 404);
        return ToResponse(error);
    }

    /// <summary>Sends one test email to the saved admin alert email. Works only while email alerts are on.</summary>
    public async Task SendTestAlertAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        if (!settings.EmailAlertsEnabled || settings.ErrorAlertEmail is not string to)
            throw new DomainException("Turn email alerts on and save before sending a test email.", 409);
        if (_sender is NoopAlertSender)
            throw new DomainException("Email isn't set up on the server yet. Add the SMTP settings, then try again.", 409);

        try
        {
            await _sender.SendAsync(
                to,
                "[Yoga Marketplace] Test alert",
                "This is a test of the error alert email. If you got it, alerts will reach you.",
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Admin {AdminId} sent a test alert email and it failed.", _current.UserId);
            throw new DomainException("The test email could not be sent. Check the SMTP settings and the server log.", 502);
        }
        _logger.LogInformation("Admin {AdminId} sent a test alert email.", _current.UserId);
    }

    private static AdminErrorEventResponse ToResponse(ErrorEvent error) => new(
        error.Id,
        error.ErrorType,
        error.Level,
        error.Source,
        error.LastMessage,
        error.LastCorrelationId,
        error.Count,
        error.FirstSeenAt,
        error.LastSeenAt,
        error.AcknowledgedAt,
        error.LastAlertedAt);
}

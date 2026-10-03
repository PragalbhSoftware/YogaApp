using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Api.Security;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserSummary>> ListAsync(string? query, string? role, CancellationToken cancellationToken);

    Task<AdminUserDetail> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminUserDetail> BlockAsync(Guid id, AdminBlockUserRequest request, CancellationToken cancellationToken);

    Task<AdminUserDetail> UnblockAsync(Guid id, AdminBlockUserRequest request, CancellationToken cancellationToken);
}

public class AdminUserService : IAdminUserService
{
    private readonly YogaDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(YogaDbContext db, ICurrentUser current, ILogger<AdminUserService> logger)
    {
        _db = db;
        _current = current;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminUserSummary>> ListAsync(
        string? query,
        string? role,
        CancellationToken cancellationToken)
    {
        var users = Users();
        if (string.IsNullOrWhiteSpace(role))
            users = users.Where(u => u.Role == UserRole.Customer || u.Role == UserRole.Provider);
        else
        {
            var parsed = AdminQuery.ParseRequired<UserRole>(role, "Unknown role.");
            users = users.Where(u => u.Role == parsed);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            var digits = new string(query.Where(char.IsDigit).ToArray());
            users = digits.Length >= 4
                ? users.Where(u =>
                    (u.Name != null && u.Name.ToLower().Contains(term))
                    || (u.Provider != null && u.Provider.DisplayName.ToLower().Contains(term))
                    || u.Phone.ToLower().Contains(term)
                    || u.Phone.Contains(digits))
                : users.Where(u =>
                    (u.Name != null && u.Name.ToLower().Contains(term))
                    || (u.Provider != null && u.Provider.DisplayName.ToLower().Contains(term))
                    || u.Phone.ToLower().Contains(term));
        }

        var rows = await users
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Phone)
            .Take(AdminQuery.PageSize)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSummary).ToList();
    }

    public async Task<AdminUserDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await Users().SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new DomainException("User not found.", 404);
        return await ToDetailAsync(user, cancellationToken);
    }

    public Task<AdminUserDetail> BlockAsync(Guid id, AdminBlockUserRequest request, CancellationToken cancellationToken) =>
        ChangeBlockAsync(id, (user, adminId, now) => UserBlocking.Block(user, adminId, request.Reason, now), cancellationToken);

    public Task<AdminUserDetail> UnblockAsync(Guid id, AdminBlockUserRequest request, CancellationToken cancellationToken) =>
        ChangeBlockAsync(id, (user, adminId, now) => UserBlocking.Unblock(user, adminId, request.Reason, now), cancellationToken);

    private async Task<AdminUserDetail> ChangeBlockAsync(
        Guid id,
        Func<User, Guid, DateTimeOffset, UserBlockEvent> change,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Provider)
            .ThenInclude(p => p!.Area)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new DomainException("User not found.", 404);

        var adminId = _current.UserId;
        var entry = change(user, adminId, DateTimeOffset.UtcNow);
        _db.UserBlockEvents.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} {Action} user {UserId}.", adminId, entry.Action, user.Id);
        return await ToDetailAsync(user, cancellationToken);
    }

    private IQueryable<User> Users() =>
        _db.Users.AsNoTracking()
            .Include(u => u.Provider)
            .ThenInclude(p => p!.Area);

    private static AdminUserSummary ToSummary(User user) => new(
        user.Id,
        user.Name,
        user.Phone,
        user.Gender?.ToString(),
        user.Email,
        user.Role.ToString(),
        user.CreatedAt,
        user.Provider is null
            ? null
            : new AdminUserProvider(
                user.Provider.Id,
                user.Provider.DisplayName,
                user.Provider.Status.ToString(),
                user.Provider.Area?.Name ?? ""),
        user.IsBlocked);

    private async Task<AdminUserDetail> ToDetailAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Provider is Provider provider)
            provider.User = user;

        var history = await (
                from entry in _db.UserBlockEvents.AsNoTracking()
                join admin in _db.Users.AsNoTracking() on entry.AdminUserId equals admin.Id
                where entry.UserId == user.Id
                select new { entry, admin.Name })
            .ToListAsync(cancellationToken);

        return new AdminUserDetail(
            user.Id,
            user.Name,
            user.Phone,
            user.Gender?.ToString(),
            user.Email,
            user.Role.ToString(),
            user.CreatedAt,
            user.Provider is null ? null : AdminProviderService.ToResponse(user.Provider),
            user.IsBlocked,
            user.BlockedAt,
            user.BlockedReason,
            history
                .OrderByDescending(row => row.entry.CreatedAt)
                .Select(row => new AdminUserBlockEventResponse(
                    row.entry.Id,
                    row.entry.Action.ToString(),
                    row.entry.Reason,
                    row.entry.AdminUserId,
                    row.Name,
                    row.entry.CreatedAt))
                .ToList());
    }
}

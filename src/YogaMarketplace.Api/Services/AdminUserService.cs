using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Domain;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Services;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserSummary>> ListAsync(string? query, string? role, CancellationToken cancellationToken);

    Task<AdminUserDetail> GetAsync(Guid id, CancellationToken cancellationToken);
}

public class AdminUserService : IAdminUserService
{
    private readonly YogaDbContext _db;

    public AdminUserService(YogaDbContext db)
    {
        _db = db;
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
        return ToDetail(user);
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
                user.Provider.Area?.Name ?? ""));

    private static AdminUserDetail ToDetail(User user)
    {
        if (user.Provider is Provider provider)
            provider.User = user;

        return new AdminUserDetail(
            user.Id,
            user.Name,
            user.Phone,
            user.Gender?.ToString(),
            user.Email,
            user.Role.ToString(),
            user.CreatedAt,
            user.Provider is null ? null : AdminProviderService.ToResponse(user.Provider));
    }
}

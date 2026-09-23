using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin.Users;

public class UserModel : PageModel
{
    private readonly IAdminUserApi _users;

    public UserModel(IAdminUserApi users)
    {
        _users = users;
    }

    public AdminUserDetailDto? Account { get; private set; }
    public string? Query { get; private set; }
    public string? Role { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(Guid id, string? q, string? role, CancellationToken cancellationToken)
    {
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        Role = AdminRoleFilters.TryNormalize(role, out var canonical) ? canonical : null;
        var result = await _users.GetAsync(id, cancellationToken);
        if (!result.Ok || result.Data is null)
        {
            Error = result.Error ?? UiCopy.GenericError;
            return;
        }

        Account = result.Data;
    }
}

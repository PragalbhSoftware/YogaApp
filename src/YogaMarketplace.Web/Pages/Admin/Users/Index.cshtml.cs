using Microsoft.AspNetCore.Mvc.RazorPages;
using YogaMarketplace.Web.Copy;
using YogaMarketplace.Web.Services;

namespace YogaMarketplace.Web.Pages.Admin.Users;

public class UsersModel : PageModel
{
    private readonly IAdminUserApi _users;

    public UsersModel(IAdminUserApi users)
    {
        _users = users;
    }

    public List<AdminUserSummaryDto> Users { get; private set; } = [];
    public string? Query { get; private set; }
    public string? Role { get; private set; }
    public bool RoleKnown { get; private set; } = true;
    public string? Error { get; private set; }

    public async Task OnGetAsync(string? q, string? role, CancellationToken cancellationToken)
    {
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (!AdminRoleFilters.TryNormalize(role, out var canonical))
        {
            RoleKnown = false;
            Error = UiCopy.UnknownRole;
            Users = [];
            return;
        }

        RoleKnown = true;
        Role = canonical;
        var list = await _users.ListAsync(Query, Role, cancellationToken);
        if (!list.Ok || list.Data is null)
        {
            Error = list.Error ?? UiCopy.GenericError;
            Users = [];
            return;
        }

        Users = list.Data;
    }
}

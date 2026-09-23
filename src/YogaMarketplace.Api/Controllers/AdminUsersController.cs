using Microsoft.AspNetCore.Mvc;
using YogaMarketplace.Api.Services;

namespace YogaMarketplace.Api.Controllers;

[Route("api/admin/users")]
public class AdminUsersController : AdminControllerBase
{
    private readonly IAdminUserService _users;

    public AdminUsersController(IAdminUserService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserSummary>>> List(
        [FromQuery] string? q,
        [FromQuery] string? role,
        CancellationToken cancellationToken) =>
        Ok(await _users.ListAsync(q, role, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _users.GetAsync(id, cancellationToken));
}

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

    [HttpPost("search")]
    public async Task<ActionResult<PagedResponse<AdminUserSummary>>> Search(
        [FromBody] AdminUserSearchRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _users.SearchAsync(request ?? new AdminUserSearchRequest(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _users.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<AdminUserDetail>> Block(
        Guid id,
        [FromBody] AdminBlockUserRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _users.BlockAsync(id, request ?? new AdminBlockUserRequest(), cancellationToken));

    [HttpPost("{id:guid}/unblock")]
    public async Task<ActionResult<AdminUserDetail>> Unblock(
        Guid id,
        [FromBody] AdminBlockUserRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _users.UnblockAsync(id, request ?? new AdminBlockUserRequest(), cancellationToken));
}

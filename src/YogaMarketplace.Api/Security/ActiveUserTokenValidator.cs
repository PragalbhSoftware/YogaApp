using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using YogaMarketplace.Infrastructure.Persistence;

namespace YogaMarketplace.Api.Security;

/// <summary>
/// Rejects a valid JWT whose user has since been blocked or removed, so blocking takes effect on the next request.
/// </summary>
public static class ActiveUserTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var value = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            context.Fail("Token has no user.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<YogaDbContext>();
        var blocked = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsBlocked)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (blocked != false)
            context.Fail("Account is blocked or no longer exists.");
    }
}

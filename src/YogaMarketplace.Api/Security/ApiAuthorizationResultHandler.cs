using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using YogaMarketplace.Domain;

namespace YogaMarketplace.Api.Security;

public class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Sign in required." });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = ForbiddenMessage(policy) });
    }

    private static string ForbiddenMessage(AuthorizationPolicy policy)
    {
        var roles = policy.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .SelectMany(requirement => requirement.AllowedRoles)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (roles.Length == 1 && roles[0] == nameof(UserRole.Admin))
            return "Admin access required.";

        return "You cannot do that.";
    }
}

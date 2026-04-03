using System.Security.Claims;
using APIGateway.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APIGateway.Infrastructure.Attributes;

/// <summary>
/// Attribute for permission-based authorization.
/// UArch: Declarative security, fail fast.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Check if user is authenticated
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Authentication required",
                code = "UNAUTHORIZED"
            });
            return;
        }

        // Get user ID from claims
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Invalid user claims",
                code = "INVALID_CLAIMS"
            });
            return;
        }

        // Check permission
        var permissionService = context.HttpContext.RequestServices
            .GetService(typeof(IPermissionService)) as IPermissionService;

        if (permissionService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        var hasPermission = await permissionService.HasPermissionAsync(userId, Permission);

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new
            {
                error = "Insufficient permissions",
                code = "FORBIDDEN",
                required = Permission
            })
            {
                StatusCode = 403
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using APIGateway.NetFramework.Models;
using APIGateway.NetFramework.Services;

namespace APIGateway.NetFramework.Infrastructure
{
    /// <summary>
    /// Permission-based authorization attribute for .NET Framework.
    /// UArch: Declarative security, fail fast.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public string Permission { get; set; }

        public RequirePermissionAttribute(string permission)
        {
            Permission = permission;
        }

        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            // Check if user is authenticated
            if (!actionContext.RequestContext.Principal.Identity.IsAuthenticated)
            {
                return false;
            }

            // Get user ID from claims
            var claimsPrincipal = actionContext.RequestContext.Principal as ClaimsPrincipal;
            if (claimsPrincipal == null)
            {
                return false;
            }

            var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return false;
            }

            // Check permission (synchronous for .NET Framework)
            var permissionService = actionContext.Request.GetDependencyScope()
                .GetService(typeof(IPermissionService)) as IPermissionService;

            if (permissionService == null)
            {
                return false;
            }

            // Use Task.Run to avoid deadlock in sync context
            var hasPermission = Task.Run(async () =>
                await permissionService.HasPermissionAsync(userId, Permission)
            ).Result;

            return hasPermission;
        }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            if (!actionContext.RequestContext.Principal.Identity.IsAuthenticated)
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    new { error = "Authentication required", code = "UNAUTHORIZED" }
                );
            }
            else
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Forbidden,
                    new
                    {
                        error = "Insufficient permissions",
                        code = "FORBIDDEN",
                        required = Permission
                    }
                );
            }
        }
    }
}

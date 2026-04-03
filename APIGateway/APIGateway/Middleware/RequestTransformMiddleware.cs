using System.Text.Json;
using APIGateway.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace APIGateway.Middleware;

/// <summary>
/// Request/Response Transformation Middleware.
/// Allows modifying headers, query params, and body on-the-fly.
/// UArch: Zero-allocation where possible.
/// </summary>
public class RequestTransformMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public RequestTransformMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip for admin/auth endpoints
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Get route config
        if (!_cache.TryGetValue("GatewayRoutes", out List<Models.Route>? routes) || routes == null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            routes = await db.Routes.AsNoTracking().ToListAsync();
            _cache.Set("GatewayRoutes", routes, TimeSpan.FromDays(365));
        }

        var routeConfig = routes!.FirstOrDefault(r =>
        {
            if (r.MatchPath == "/{**catch-all}") return true;
            var matchPath = r.MatchPath.AsSpan();
            if (matchPath.EndsWith("/{**catch-all}"))
                matchPath = matchPath[..^15];
            return path.AsSpan().StartsWith(matchPath);
        });

        // Apply request transformations
        ApplyRequestTransforms(context, routeConfig);

        await _next(context);

        // Apply response transformations
        await ApplyResponseTransforms(context, routeConfig);
    }

    private void ApplyRequestTransforms(HttpContext context, Models.Route? routeConfig)
    {
        // Add correlation ID if not present
        if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
        {
            context.Request.Headers["X-Correlation-ID"] = Guid.NewGuid().ToString("N");
        }

        // Add forwarded headers
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        context.Request.Headers["X-Forwarded-For"] = clientIp;
        context.Request.Headers["X-Forwarded-Proto"] = context.Request.Scheme;
        context.Request.Headers["X-Forwarded-Host"] = context.Request.Host.ToString();

        // Add gateway identifier
        context.Request.Headers["X-Gateway"] = "APIGateway/2.0";

        // Remove sensitive headers before forwarding
        context.Request.Headers.Remove("Cookie");
        context.Request.Headers.Remove("Authorization"); // Will be re-added by auth middleware if needed

        // Custom transforms from route config (future enhancement)
        // if (routeConfig?.RequestTransforms != null)
        // {
        //     ApplyCustomTransforms(context.Request, routeConfig.RequestTransforms);
        // }
    }

    private async Task ApplyResponseTransforms(HttpContext context, Models.Route? routeConfig)
    {
        // Add security headers
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Add CORS headers (if not already set)
        if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
        {
            // Will be handled by CORS middleware
        }

        // Remove server identification headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");

        // Add gateway headers
        context.Response.Headers["X-Gateway"] = "APIGateway/2.0";

        // Echo correlation ID
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId.ToString();
        }

        // Custom transforms from route config (future enhancement)
        // if (routeConfig?.ResponseTransforms != null)
        // {
        //     await ApplyCustomResponseTransforms(context.Response, routeConfig.ResponseTransforms);
        // }
    }
}

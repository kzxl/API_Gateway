using APIGateway.Data;
using APIGateway.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/config")]
public class AdminConfigController : ControllerBase
{
    private readonly GatewayDbContext _db;

    public AdminConfigController(GatewayDbContext db) => _db = db;

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var routes = await _db.Routes.ToListAsync();
        var clusters = await _db.Clusters.ToListAsync();
        var users = await _db.Users
            .Select(u => new { u.Username, u.Role, u.IsActive })
            .ToListAsync();

        return Ok(new
        {
            exportedAt = DateTime.UtcNow,
            version = "1.0",
            routes,
            clusters,
            users
        });
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] JsonElement body)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        int routesImported = 0, clustersImported = 0;

        if (body.TryGetProperty("routes", out var routesEl))
        {
            var routes = JsonSerializer.Deserialize<List<Models.Route>>(routesEl.GetRawText(), options) ?? [];
            foreach (var r in routes)
            {
                r.Id = 0; // Let DB assign new IDs
                var existing = await _db.Routes.FirstOrDefaultAsync(x => x.RouteId == r.RouteId);
                if (existing != null)
                {
                    existing.MatchPath = r.MatchPath;
                    existing.Methods = r.Methods;
                    existing.ClusterId = r.ClusterId;
                    existing.RateLimitPerSecond = r.RateLimitPerSecond;
                    existing.CircuitBreakerThreshold = r.CircuitBreakerThreshold;
                    existing.CircuitBreakerDurationSeconds = r.CircuitBreakerDurationSeconds;
                    existing.IpWhitelist = r.IpWhitelist;
                    existing.IpBlacklist = r.IpBlacklist;
                    existing.CacheTtlSeconds = r.CacheTtlSeconds;
                    existing.TransformsJson = r.TransformsJson;
                }
                else
                {
                    _db.Routes.Add(r);
                }
                routesImported++;
            }
        }

        if (body.TryGetProperty("clusters", out var clustersEl))
        {
            var clusters = JsonSerializer.Deserialize<List<Cluster>>(clustersEl.GetRawText(), options) ?? [];
            foreach (var c in clusters)
            {
                c.Id = 0;
                var existing = await _db.Clusters.FirstOrDefaultAsync(x => x.ClusterId == c.ClusterId);
                if (existing != null)
                {
                    existing.DestinationsJson = c.DestinationsJson;
                    existing.EnableHealthCheck = c.EnableHealthCheck;
                    existing.HealthCheckPath = c.HealthCheckPath;
                    existing.HealthCheckIntervalSeconds = c.HealthCheckIntervalSeconds;
                    existing.HealthCheckTimeoutSeconds = c.HealthCheckTimeoutSeconds;
                    existing.LoadBalancingPolicy = c.LoadBalancingPolicy;
                    existing.RetryCount = c.RetryCount;
                    existing.RetryDelayMs = c.RetryDelayMs;
                }
                else
                {
                    _db.Clusters.Add(c);
                }
                clustersImported++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Import completed", routesImported, clustersImported });
    }
}

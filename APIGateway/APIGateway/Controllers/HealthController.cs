using APIGateway.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/health")]
public class HealthController : ControllerBase
{
    private readonly IRouteRepository _repo;
    private readonly DbProxyConfigProvider _provider;

    public HealthController(IRouteRepository repo, DbProxyConfigProvider provider)
    {
        _repo = repo;
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var routes = await _repo.GetRoutesAsync();
        var clusters = await _repo.GetClustersAsync();
        var proxyConfig = _provider.GetConfig();

        var destinations = clusters
            .SelectMany(c =>
            {
                try
                {
                    var dests = JsonSerializer.Deserialize<List<DestInfo>>(c.DestinationsJson);
                    return dests?.Select(d => new { c.ClusterId, d.Address }) ?? [];
                }
                catch { return []; }
            })
            .ToList();

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            gateway = new
            {
                totalRoutes = routes.Count,
                totalClusters = clusters.Count,
                totalDestinations = destinations.Count,
                activeProxyRoutes = proxyConfig.Routes.Count,
                activeProxyClusters = proxyConfig.Clusters.Count,
            },
            destinations
        });
    }

    private record DestInfo(string Id, string Address);
}

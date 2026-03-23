using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace APIGateway.Services;

public class DbProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    private volatile InMemoryConfig _currentConfig;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Timer _timer;
    private CancellationTokenSource _changeTokenSource = new();

    public DbProxyConfigProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _currentConfig = LoadConfigFromDb();
        _timer = new Timer(_ => CheckForChanges(), null, 5000, 5000);
    }

    public IProxyConfig GetConfig() => _currentConfig;

    private void CheckForChanges()
    {
        try
        {
            var newCfg = LoadConfigFromDb();
            if (!ProxyConfigEquals(_currentConfig, newCfg))
            {
                var oldCts = _changeTokenSource;
                _changeTokenSource = new CancellationTokenSource();
                _currentConfig = newCfg;
                oldCts.Cancel();
                oldCts.Dispose();
            }
        }
        catch { /* DB temporarily unavailable */ }
    }

    private InMemoryConfig LoadConfigFromDb()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();
            var routes = repo.GetRoutesAsync().GetAwaiter().GetResult();
            var clusters = repo.GetClustersAsync().GetAwaiter().GetResult();

            var routeConfigs = routes.Select(r => new RouteConfig
            {
                RouteId = r.RouteId,
                Match = new RouteMatch
                {
                    Path = r.MatchPath,
                    Methods = string.IsNullOrWhiteSpace(r.Methods)
                        ? null
                        : r.Methods.Split(',', StringSplitOptions.RemoveEmptyEntries)
                },
                ClusterId = r.ClusterId
            }).ToList();

            var clusterList = clusters.Select(c =>
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dests = JsonSerializer.Deserialize<List<DestinationDto>>(c.DestinationsJson, jsonOptions) ?? [];

                var destinationConfigs = dests.ToDictionary(
                    d => d.Id,
                    d => new DestinationConfig
                    {
                        Address = d.Address,
                        Health = d.Health ?? "Active" // "Active" or "Standby"
                    });

                var config = new ClusterConfig
                {
                    ClusterId = c.ClusterId,
                    LoadBalancingPolicy = c.LoadBalancingPolicy ?? "RoundRobin",
                    Destinations = destinationConfigs,
                };

                // Enable health checks if configured
                if (c.EnableHealthCheck)
                {
                    config = config with
                    {
                        HealthCheck = new HealthCheckConfig
                        {
                            Active = new ActiveHealthCheckConfig
                            {
                                Enabled = true,
                                Interval = TimeSpan.FromSeconds(c.HealthCheckIntervalSeconds > 0 ? c.HealthCheckIntervalSeconds : 10),
                                Timeout = TimeSpan.FromSeconds(c.HealthCheckTimeoutSeconds > 0 ? c.HealthCheckTimeoutSeconds : 5),
                                Path = c.HealthCheckPath ?? "/health",
                                Policy = "ConsecutiveFailures"
                            },
                            Passive = new PassiveHealthCheckConfig
                            {
                                Enabled = true,
                                Policy = "TransportFailureRate",
                                ReactivationPeriod = TimeSpan.FromSeconds(30)
                            }
                        }
                    };
                }

                return config;
            }).ToList();

            return new InMemoryConfig(
                routeConfigs,
                clusterList,
                new CancellationChangeToken(_changeTokenSource.Token));
        }
        catch
        {
            return new InMemoryConfig(
                [],
                [],
                new CancellationChangeToken(_changeTokenSource.Token));
        }
    }

    private bool ProxyConfigEquals(InMemoryConfig a, InMemoryConfig b)
    {
        if (a == null || b == null) return false;
        return JsonSerializer.Serialize(a.Routes) == JsonSerializer.Serialize(b.Routes)
            && JsonSerializer.Serialize(a.Clusters) == JsonSerializer.Serialize(b.Clusters);
    }

    public void ForceReload()
    {
        var oldCts = _changeTokenSource;
        _changeTokenSource = new CancellationTokenSource();
        _currentConfig = LoadConfigFromDb();
        oldCts.Cancel();
        oldCts.Dispose();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _changeTokenSource.Dispose();
    }

    private record DestinationDto(string Id, string Address, string? Health);

    private sealed class InMemoryConfig : IProxyConfig
    {
        public InMemoryConfig(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            IChangeToken token)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = token;
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }
    }
}

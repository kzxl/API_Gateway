using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace APIGateway.Services
{
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
            var newCfg = LoadConfigFromDb();
            if (!ProxyConfigEquals(_currentConfig, newCfg))
            {
                _changeTokenSource.Cancel();
                _changeTokenSource = new CancellationTokenSource();
                _currentConfig = newCfg;
            }
        }

        private InMemoryConfig LoadConfigFromDb()
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
                var dests = JsonSerializer.Deserialize<List<Destination>>(c.DestinationsJson) ?? new();
                return new ClusterConfig
                {
                    ClusterId = c.ClusterId,
                    Destinations = dests.ToDictionary(d => d.Id, d => new DestinationConfig { Address = d.Address })
                };
            }).ToList();

            return new InMemoryConfig(routeConfigs, clusterList, new CancellationChangeToken(_changeTokenSource.Token));

        }

        private bool ProxyConfigEquals(InMemoryConfig a, InMemoryConfig b)
        {
            if (a == null || b == null) return false;
            return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
        }

        public void ForceReload()
        {
            _changeTokenSource.Cancel();
            _changeTokenSource = new CancellationTokenSource();
            _currentConfig = LoadConfigFromDb();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _changeTokenSource.Dispose();
        }

        private record Destination(string Id, string Address);

        private sealed class InMemoryConfig : IProxyConfig
        {
            public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, IChangeToken token)
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
}

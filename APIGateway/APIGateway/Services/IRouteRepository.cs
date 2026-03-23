using APIGateway.Models;

namespace APIGateway.Services;

public interface IRouteRepository
{
    // Routes
    Task<List<Models.Route>> GetRoutesAsync();
    Task<Models.Route?> GetRouteByIdAsync(int id);
    Task AddOrUpdateRouteAsync(Models.Route r);
    Task DeleteRouteAsync(int id);

    // Clusters
    Task<List<Cluster>> GetClustersAsync();
    Task<Cluster?> GetClusterByIdAsync(int id);
    Task AddOrUpdateClusterAsync(Cluster c);
    Task DeleteClusterAsync(int id);
}

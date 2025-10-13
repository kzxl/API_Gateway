using APIGateway.Models;

namespace APIGateway.Services
{
    public interface IRouteRepository
    {
        Task<List<Models.Route>> GetRoutesAsync();
        Task<List<Cluster>> GetClustersAsync();
        Task AddOrUpdateRouteAsync(Models.Route r);
        Task DeleteRouteAsync(int id);
        Task AddOrUpdateClusterAsync(Cluster c);
    }
}

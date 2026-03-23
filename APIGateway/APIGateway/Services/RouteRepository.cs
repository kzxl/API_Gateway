using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Services;

public class RouteRepository : IRouteRepository
{
    private readonly GatewayDbContext _db;
    public RouteRepository(GatewayDbContext db) => _db = db;

    // ---- Routes ----

    public async Task<List<Models.Route>> GetRoutesAsync()
        => await _db.Routes.AsNoTracking().ToListAsync();

    public async Task<Models.Route?> GetRouteByIdAsync(int id)
        => await _db.Routes.FindAsync(id);

    public async Task AddOrUpdateRouteAsync(Models.Route r)
    {
        if (r.Id == 0)
            _db.Routes.Add(r);
        else
            _db.Routes.Update(r);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteRouteAsync(int id)
    {
        var entity = await _db.Routes.FindAsync(id);
        if (entity != null)
        {
            _db.Routes.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    // ---- Clusters ----

    public async Task<List<Cluster>> GetClustersAsync()
        => await _db.Clusters.AsNoTracking().ToListAsync();

    public async Task<Cluster?> GetClusterByIdAsync(int id)
        => await _db.Clusters.FindAsync(id);

    public async Task AddOrUpdateClusterAsync(Cluster c)
    {
        if (c.Id == 0)
            _db.Clusters.Add(c);
        else
            _db.Clusters.Update(c);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteClusterAsync(int id)
    {
        var entity = await _db.Clusters.FindAsync(id);
        if (entity != null)
        {
            _db.Clusters.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}

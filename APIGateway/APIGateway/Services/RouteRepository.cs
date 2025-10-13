using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Services
{
    public class RouteRepository : IRouteRepository
    {
        private readonly GatewayDbContext _db;
        public RouteRepository(GatewayDbContext db) { _db = db; }
        public async Task<List<Models.Route>> GetRoutesAsync() => await _db.Routes.AsNoTracking().ToListAsync();
        public async Task<List<Cluster>> GetClustersAsync() => await _db.Clusters.AsNoTracking().ToListAsync();
        public async Task AddOrUpdateRouteAsync(Models.Route r)
        {
            if (r.Id == 0) _db.Routes.Add(r); else _db.Routes.Update(r);
            await _db.SaveChangesAsync();
        }
        public async Task DeleteRouteAsync(int id)
        {
            var e = await _db.Routes.FindAsync(id);
            if (e != null) { _db.Routes.Remove(e); await _db.SaveChangesAsync(); }
        }
        public async Task AddOrUpdateClusterAsync(Cluster c)
        {
            if (c.Id == 0) _db.Clusters.Add(c); else _db.Clusters.Update(c);
            await _db.SaveChangesAsync();
        }
    }
}

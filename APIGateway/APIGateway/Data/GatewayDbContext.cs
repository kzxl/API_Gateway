using APIGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Data
{
    public class GatewayDbContext : DbContext
    {
        public GatewayDbContext(DbContextOptions<GatewayDbContext> opts) : base(opts) { }
        public DbSet<Models.Route> Routes { get; set; }
        public DbSet<Cluster> Clusters { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RequestLog> RequestLogs { get; set; }
    }
}

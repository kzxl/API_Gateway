using APIGateway.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace APIGateway.Data
{
    public class GatewayDbContext : DbContext
    {
        public GatewayDbContext(DbContextOptions<GatewayDbContext> opts) : base(opts) { }
        public DbSet<Models.Route> Routes { get; set; }
        public DbSet<Cluster> Clusters { get; set; }
    }
}

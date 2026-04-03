using System.Data.Entity;
using APIGateway.NetFramework.Models;

namespace APIGateway.NetFramework.Data
{
    public class GatewayDbContext : DbContext
    {
        public GatewayDbContext() : base("GatewayDb")
        {
            // Disable lazy loading for performance
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Route> Routes { get; set; }
        public DbSet<Cluster> Clusters { get; set; }
        public DbSet<RequestLog> RequestLogs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // RefreshToken configuration
            modelBuilder.Entity<RefreshToken>()
                .HasRequired(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            // UserSession configuration
            modelBuilder.Entity<UserSession>()
                .HasRequired(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.SessionId)
                .IsUnique();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.AccessTokenJti);

            // Permission configuration
            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Name)
                .IsUnique();

            // RolePermission configuration
            modelBuilder.Entity<RolePermission>()
                .HasRequired(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.Role, rp.PermissionId })
                .IsUnique();

            // UserPermission configuration
            modelBuilder.Entity<UserPermission>()
                .HasRequired(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<UserPermission>()
                .HasRequired(up => up.Permission)
                .WithMany()
                .HasForeignKey(up => up.PermissionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<UserPermission>()
                .HasIndex(up => new { up.UserId, up.PermissionId })
                .IsUnique();

            // Route configuration
            modelBuilder.Entity<Route>()
                .HasIndex(r => r.RouteId)
                .IsUnique();

            // Cluster configuration
            modelBuilder.Entity<Cluster>()
                .HasIndex(c => c.ClusterId)
                .IsUnique();

            // RequestLog configuration
            modelBuilder.Entity<RequestLog>()
                .HasIndex(l => l.Timestamp);

            modelBuilder.Entity<RequestLog>()
                .HasIndex(l => l.RouteId);
        }
    }
}

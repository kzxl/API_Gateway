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
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // RefreshToken indexes for performance
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.UserId);

            // UserSession indexes
            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.SessionId)
                .IsUnique();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.AccessTokenJti);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.UserId);

            // Permission indexes
            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Name)
                .IsUnique();

            modelBuilder.Entity<Permission>()
                .HasIndex(p => new { p.Resource, p.Action });

            // RolePermission indexes
            modelBuilder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.Role, rp.PermissionId })
                .IsUnique();

            // UserPermission indexes
            modelBuilder.Entity<UserPermission>()
                .HasIndex(up => new { up.UserId, up.PermissionId })
                .IsUnique();

            // Relationships
            modelBuilder.Entity<RefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.Permission)
                .WithMany()
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using APIGateway.NetFramework.Data;
using APIGateway.NetFramework.Models;

namespace APIGateway.NetFramework.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly GatewayDbContext _db;

        // L1 cache for permissions
        private static readonly ConcurrentDictionary<string, List<string>> _rolePermissionsCache = new ConcurrentDictionary<string, List<string>>();
        private static readonly ConcurrentDictionary<int, List<string>> _userPermissionsCache = new ConcurrentDictionary<int, List<string>>();

        public PermissionService(GatewayDbContext db)
        {
            _db = db;
        }

        public async Task<bool> HasPermissionAsync(int userId, string permissionName)
        {
            // Check user-specific permissions cache
            if (_userPermissionsCache.TryGetValue(userId, out var userPerms))
            {
                if (userPerms.Contains(permissionName))
                {
                    return true;
                }
            }
            else
            {
                // Load user permissions
                var permissions = await _db.UserPermissions
                    .Where(up => up.UserId == userId && up.IsGranted)
                    .Include(up => up.Permission)
                    .Select(up => up.Permission.Name)
                    .ToListAsync();

                _userPermissionsCache.TryAdd(userId, permissions);

                if (permissions.Contains(permissionName))
                {
                    return true;
                }
            }

            // Check role permissions
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                return await HasPermissionAsync(user.Role, permissionName);
            }

            return false;
        }

        public async Task<bool> HasPermissionAsync(string role, string permissionName)
        {
            // Check cache
            if (_rolePermissionsCache.TryGetValue(role, out var permissions))
            {
                return permissions.Contains(permissionName);
            }

            // Load from database
            var rolePermissions = await _db.RolePermissions
                .Where(rp => rp.Role == role)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            _rolePermissionsCache.TryAdd(role, rolePermissions);

            return rolePermissions.Contains(permissionName);
        }

        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _db.Permissions.ToListAsync();
        }

        public async Task<List<Permission>> GetRolePermissionsAsync(string role)
        {
            return await _db.RolePermissions
                .Where(rp => rp.Role == role)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<List<Permission>> GetUserPermissionsAsync(int userId)
        {
            return await _db.UserPermissions
                .Where(up => up.UserId == userId && up.IsGranted)
                .Include(up => up.Permission)
                .Select(up => up.Permission)
                .ToListAsync();
        }

        public async Task GrantPermissionToRoleAsync(string role, int permissionId)
        {
            var exists = await _db.RolePermissions
                .AnyAsync(rp => rp.Role == role && rp.PermissionId == permissionId);

            if (!exists)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    Role = role,
                    PermissionId = permissionId
                });
                await _db.SaveChangesAsync();

                // Invalidate cache
                _rolePermissionsCache.TryRemove(role, out _);
            }
        }

        public async Task RevokePermissionFromRoleAsync(string role, int permissionId)
        {
            var rolePermission = await _db.RolePermissions
                .FirstOrDefaultAsync(rp => rp.Role == role && rp.PermissionId == permissionId);

            if (rolePermission != null)
            {
                _db.RolePermissions.Remove(rolePermission);
                await _db.SaveChangesAsync();

                // Invalidate cache
                _rolePermissionsCache.TryRemove(role, out _);
            }
        }
    }
}

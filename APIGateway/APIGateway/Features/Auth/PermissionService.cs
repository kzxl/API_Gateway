using System.Collections.Concurrent;
using APIGateway.Core.Interfaces;
using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace APIGateway.Features.Auth;

/// <summary>
/// Permission service with L1 cache for high performance.
/// UArch: Zero-allocation hot path, nanosecond permission checks.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly GatewayDbContext _db;
    private readonly IMemoryCache _cache;

    // L1 Cache: Permission lookups (in-memory, fast)
    private static readonly ConcurrentDictionary<string, HashSet<string>> _rolePermissionsCache = new();
    private static readonly ConcurrentDictionary<int, HashSet<string>> _userPermissionsCache = new();

    public PermissionService(GatewayDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permissionName)
    {
        // Check user-specific permissions first (overrides)
        if (_userPermissionsCache.TryGetValue(userId, out var userPerms))
        {
            if (userPerms.Contains(permissionName))
                return true;
        }
        else
        {
            // Load user permissions
            var permissions = await GetUserPermissionsAsync(userId);
            var permSet = new HashSet<string>(permissions.Select(p => p.Name));
            _userPermissionsCache.TryAdd(userId, permSet);

            if (permSet.Contains(permissionName))
                return true;
        }

        // Check role permissions
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;

        return await HasPermissionAsync(user.Role, permissionName);
    }

    public async Task<bool> HasPermissionAsync(string role, string permissionName)
    {
        // L1 Cache check
        if (_rolePermissionsCache.TryGetValue(role, out var permissions))
        {
            return permissions.Contains(permissionName);
        }

        // Load from database
        var rolePermissions = await GetRolePermissionsAsync(role);
        var permSet = new HashSet<string>(rolePermissions.Select(p => p.Name));
        _rolePermissionsCache.TryAdd(role, permSet);

        return permSet.Contains(permissionName);
    }

    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        return await _db.Permissions.AsNoTracking().ToListAsync();
    }

    public async Task<List<Permission>> GetRolePermissionsAsync(string role)
    {
        return await _db.RolePermissions
            .Where(rp => rp.Role == role)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Permission>> GetUserPermissionsAsync(int userId)
    {
        return await _db.UserPermissions
            .Where(up => up.UserId == userId && up.IsGranted)
            .Include(up => up.Permission)
            .Select(up => up.Permission)
            .AsNoTracking()
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

    public async Task GrantPermissionToUserAsync(int userId, int permissionId)
    {
        var exists = await _db.UserPermissions
            .AnyAsync(up => up.UserId == userId && up.PermissionId == permissionId);

        if (!exists)
        {
            _db.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permissionId,
                IsGranted = true
            });
            await _db.SaveChangesAsync();

            // Invalidate cache
            _userPermissionsCache.TryRemove(userId, out _);
        }
    }

    public async Task RevokePermissionFromUserAsync(int userId, int permissionId)
    {
        var userPermission = await _db.UserPermissions
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permissionId);

        if (userPermission != null)
        {
            _db.UserPermissions.Remove(userPermission);
            await _db.SaveChangesAsync();

            // Invalidate cache
            _userPermissionsCache.TryRemove(userId, out _);
        }
    }
}

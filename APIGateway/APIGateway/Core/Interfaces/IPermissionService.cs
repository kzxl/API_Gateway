namespace APIGateway.Core.Interfaces;

/// <summary>
/// Permission service for fine-grained access control.
/// UArch: Contract-first DI pattern.
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permissionName);
    Task<bool> HasPermissionAsync(string role, string permissionName);
    Task<List<Models.Permission>> GetAllPermissionsAsync();
    Task<List<Models.Permission>> GetRolePermissionsAsync(string role);
    Task<List<Models.Permission>> GetUserPermissionsAsync(int userId);
    Task GrantPermissionToRoleAsync(string role, int permissionId);
    Task RevokePermissionFromRoleAsync(string role, int permissionId);
    Task GrantPermissionToUserAsync(int userId, int permissionId);
    Task RevokePermissionFromUserAsync(int userId, int permissionId);
}

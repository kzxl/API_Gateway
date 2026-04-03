using System.Threading.Tasks;
using APIGateway.NetFramework.DTOs;

namespace APIGateway.NetFramework.Services
{
    public interface IUserService
    {
        Task<UserDto> GetByIdAsync(int id);
        Task<UserDto> GetByUsernameAsync(string username);
        Task<UserDto> ValidateCredentialsAsync(string username, string password);
        Task IncrementFailedLoginAsync(int userId);
        Task ResetFailedLoginAsync(int userId);
        Task LockAccountAsync(int userId, System.TimeSpan duration);
    }

    public interface ITokenService
    {
        Task<Models.RefreshToken> CreateRefreshTokenAsync(int userId, string ipAddress);
        Task<Models.RefreshToken> ValidateRefreshTokenAsync(string token);
        Task<Models.RefreshToken> RotateRefreshTokenAsync(Models.RefreshToken oldToken, string ipAddress);
        Task RevokeRefreshTokenAsync(string token, string ipAddress);
        Task<bool> IsAccessTokenBlacklistedAsync(string jti);
        Task BlacklistAccessTokenAsync(string jti, System.DateTime expiresAt);
        Task UpdateSessionActivityAsync(string jti);
    }

    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(int userId, string permissionName);
        Task<bool> HasPermissionAsync(string role, string permissionName);
        Task<System.Collections.Generic.List<Models.Permission>> GetAllPermissionsAsync();
        Task<System.Collections.Generic.List<Models.Permission>> GetRolePermissionsAsync(string role);
        Task<System.Collections.Generic.List<Models.Permission>> GetUserPermissionsAsync(int userId);
        Task GrantPermissionToRoleAsync(string role, int permissionId);
        Task RevokePermissionFromRoleAsync(string role, int permissionId);
    }

    public interface IRouteService
    {
        Task<System.Collections.Generic.List<Models.Route>> GetAllAsync();
        Task<Models.Route> GetByIdAsync(int id);
        Task<Models.Route> CreateAsync(CreateRouteDto dto);
        Task<Models.Route> UpdateAsync(int id, UpdateRouteDto dto);
        Task DeleteAsync(int id);
    }

    public interface IClusterService
    {
        Task<System.Collections.Generic.List<Models.Cluster>> GetAllAsync();
        Task<Models.Cluster> GetByIdAsync(int id);
    }

    public interface ILogService
    {
        Task<System.Collections.Generic.List<Models.RequestLog>> GetLogsAsync(int skip, int take);
        Task<long> GetTotalCountAsync();
    }
}

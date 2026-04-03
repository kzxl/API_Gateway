using APIGateway.Core.Interfaces.DTOs;

namespace APIGateway.Core.Interfaces;

public interface IRouteService
{
    Task<List<RouteDto>> GetAllAsync();
    Task<RouteDto?> GetByIdAsync(int id);
    Task<RouteDto> CreateAsync(CreateRouteDto dto);
    Task<RouteDto?> UpdateAsync(int id, CreateRouteDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IClusterService
{
    Task<List<ClusterDto>> GetAllAsync();
    Task<ClusterDto?> GetByIdAsync(int id);
    Task<ClusterDto> CreateAsync(CreateClusterDto dto);
    Task<ClusterDto?> UpdateAsync(int id, CreateClusterDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteAsync(int id);
    Task<UserDto?> ValidateCredentialsAsync(string username, string password);
    Task<UserDto?> GetByIdAsync(int id);
    Task<UserDto?> GetByUsernameAsync(string username);
    Task IncrementFailedLoginAsync(int userId);
    Task ResetFailedLoginAsync(int userId);
    Task LockAccountAsync(int userId, TimeSpan duration);
}

public interface ILogService
{
    Task<LogPageDto> GetLogsAsync(int page, int pageSize, string? routeId, int? statusCode, string? method);
    Task<LogStatsDto> GetStatsAsync();
    Task ClearAsync();
}

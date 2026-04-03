namespace APIGateway.Core.Interfaces.DTOs;

// ── Route DTOs ──
public record RouteDto(
    int Id, string RouteId, string MatchPath, string? Methods, string ClusterId,
    int RateLimitPerSecond, int CircuitBreakerThreshold, int CircuitBreakerDurationSeconds,
    string? IpWhitelist, string? IpBlacklist, int CacheTtlSeconds, string? TransformsJson);

public record CreateRouteDto(
    string RouteId, string MatchPath, string? Methods, string ClusterId,
    int RateLimitPerSecond = 0, int CircuitBreakerThreshold = 0, int CircuitBreakerDurationSeconds = 30,
    string? IpWhitelist = null, string? IpBlacklist = null, int CacheTtlSeconds = 0, string? TransformsJson = null);

// ── Cluster DTOs ──
public record ClusterDto(
    int Id, string ClusterId, string DestinationsJson,
    bool EnableHealthCheck, string HealthCheckPath, int HealthCheckIntervalSeconds, int HealthCheckTimeoutSeconds,
    string LoadBalancingPolicy, int RetryCount, int RetryDelayMs);

public record CreateClusterDto(
    string ClusterId, string DestinationsJson,
    bool EnableHealthCheck = true, string HealthCheckPath = "/health",
    int HealthCheckIntervalSeconds = 10, int HealthCheckTimeoutSeconds = 5,
    string LoadBalancingPolicy = "RoundRobin", int RetryCount = 0, int RetryDelayMs = 1000);

// ── User DTOs ──
public record UserDto(int Id, string Username, string Role, bool IsActive, DateTime CreatedAt)
{
    public int FailedLoginAttempts { get; init; }
    public DateTime? LockedUntil { get; init; }
    public bool IsLocked { get; init; }
}

public record CreateUserDto(string Username, string Password, string? Role = "User");
public record UpdateUserDto(string? Username = null, string? Password = null, string? Role = null, bool? IsActive = null);

// ── Log DTOs ──
public record LogPageDto(int Total, int Page, int PageSize, List<LogEntryDto> Logs);
public record LogEntryDto(int Id, DateTime Timestamp, string Method, string Path, int StatusCode, long LatencyMs, string? ClientIp, string? RouteId, string? UserAgent);
public record LogStatsDto(int Total, int Last24h, List<StatusGroupDto> ByStatus, List<TopRouteDto> TopRoutes);
public record StatusGroupDto(string StatusGroup, int Count);
public record TopRouteDto(string? RouteId, int Count, double AvgLatency);

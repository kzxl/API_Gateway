namespace APIGateway.Core.Constants;

/// <summary>User roles in the system</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>Destination health/role status</summary>
public static class HealthStatus
{
    public const string Active = "Active";   // Primary destination
    public const string Standby = "Standby"; // Failover destination
}

/// <summary>Load balancing policies supported by YARP</summary>
public static class LoadBalancing
{
    public const string RoundRobin = "RoundRobin";
    public const string Random = "Random";
    public const string LeastRequests = "LeastRequests";
    public const string PowerOfTwoChoices = "PowerOfTwoChoices";
}

/// <summary>Default configuration values</summary>
public static class Defaults
{
    public const int RateLimitPerSecond = 0;
    public const int CircuitBreakerThreshold = 0;
    public const int CircuitBreakerDurationSeconds = 30;
    public const int CacheTtlSeconds = 0;
    public const int HealthCheckIntervalSeconds = 10;
    public const int HealthCheckTimeoutSeconds = 5;
    public const string HealthCheckPath = "/health";
    public const int RetryCount = 0;
    public const int RetryDelayMs = 1000;
}

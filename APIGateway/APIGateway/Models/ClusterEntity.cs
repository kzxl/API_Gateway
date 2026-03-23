using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class Cluster
{
    [Key]
    public int Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// JSON array: [{"id":"dest-1","address":"http://host:port","health":"Active"}]
    /// </summary>
    public string DestinationsJson { get; set; } = "[]";

    // ── Health Check ──
    public bool EnableHealthCheck { get; set; } = true;
    public string HealthCheckPath { get; set; } = "/health";
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    // ── Load Balancing ──
    public string LoadBalancingPolicy { get; set; } = "RoundRobin";

    // ── Retry Policy ──
    /// <summary>Number of retries on failure. 0 = no retry</summary>
    public int RetryCount { get; set; } = 0;
    public int RetryDelayMs { get; set; } = 1000;
}

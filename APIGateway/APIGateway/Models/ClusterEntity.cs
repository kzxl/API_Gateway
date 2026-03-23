using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class Cluster
{
    [Key]
    public int Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// JSON array: [{"id":"dest-1","address":"http://host:port","health":"Active"}]
    /// health: "Active" (primary) | "Standby" (failover only)
    /// </summary>
    public string DestinationsJson { get; set; } = "[]";

    // ── Health Check Config ──
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>Health probe path on each destination, e.g. "/health"</summary>
    public string HealthCheckPath { get; set; } = "/health";

    /// <summary>Probe interval in seconds</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;

    /// <summary>Probe timeout in seconds</summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>Load balancing policy: RoundRobin, Random, LeastRequests, FirstAlphabetical, PowerOfTwoChoices</summary>
    public string LoadBalancingPolicy { get; set; } = "RoundRobin";
}

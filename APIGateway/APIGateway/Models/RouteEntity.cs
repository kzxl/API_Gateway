using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class Route
{
    [Key]
    public int Id { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string MatchPath { get; set; } = string.Empty;
    public string? Methods { get; set; } // comma separated, nullable
    public string ClusterId { get; set; } = string.Empty;

    // ── Rate Limiting ──
    /// <summary>Max requests per second per client IP. 0 = unlimited</summary>
    public int RateLimitPerSecond { get; set; } = 0;

    // ── Circuit Breaker ──
    /// <summary>Error rate threshold (%) to trip circuit. 0 = disabled</summary>
    public int CircuitBreakerThreshold { get; set; } = 0;
    /// <summary>Duration in seconds to keep circuit open</summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    // ── IP Filter ──
    /// <summary>Comma-separated allowed IPs/CIDRs. Empty = allow all</summary>
    public string? IpWhitelist { get; set; }
    /// <summary>Comma-separated blocked IPs/CIDRs</summary>
    public string? IpBlacklist { get; set; }

    // ── Response Caching ──
    /// <summary>Cache TTL for GET requests in seconds. 0 = no cache</summary>
    public int CacheTtlSeconds { get; set; } = 0;

    // ── Transforms ──
    /// <summary>JSON: transforms config for YARP</summary>
    public string? TransformsJson { get; set; }
}

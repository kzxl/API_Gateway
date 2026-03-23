using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class RequestLog
{
    [Key]
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public string? ClientIp { get; set; }
    public string? RouteId { get; set; }
    public string? UserAgent { get; set; }
}

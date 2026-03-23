using APIGateway.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/metrics")]
public class MetricsController : ControllerBase
{
    /// <summary>
    /// Get per-route traffic metrics: throughput, latency, error rate
    /// </summary>
    [HttpGet]
    public IActionResult GetMetrics()
    {
        var metrics = MetricsMiddleware.GetAllMetrics();
        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            routeCount = metrics.Count,
            routes = metrics
        });
    }

    /// <summary>
    /// Reset all metrics counters
    /// </summary>
    [HttpDelete]
    public IActionResult ResetMetrics()
    {
        MetricsMiddleware.Reset();
        return Ok(new { message = "Metrics reset" });
    }
}

using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

/// <summary>
/// Performance monitoring and control controller.
/// </summary>
[ApiController]
[Route("admin/performance")]
public class PerformanceController : ControllerBase
{
    /// <summary>
    /// Get throughput statistics for all routes.
    /// </summary>
    [HttpGet("throughput")]
    public IActionResult GetThroughput()
    {
        var stats = Middleware.ThroughputControlMiddleware.GetThroughputStats();
        return Ok(new
        {
            routes = stats,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    [HttpGet("cache")]
    public IActionResult GetCacheStats()
    {
        var stats = Middleware.ResponseCachingMiddleware.GetStatistics();
        return Ok(new
        {
            cache = stats,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get retry statistics.
    /// </summary>
    [HttpGet("retry")]
    public IActionResult GetRetryStats()
    {
        var stats = Middleware.RequestRetryMiddleware.GetStatistics();
        return Ok(new
        {
            retry = stats,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get circuit breaker states.
    /// </summary>
    [HttpGet("circuit-breaker")]
    public IActionResult GetCircuitBreakerStates()
    {
        var states = Middleware.GatewayProtectionMiddleware.GetCircuitStates();
        return Ok(new
        {
            circuits = states,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get comprehensive performance metrics.
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        return Ok(new
        {
            throughput = Middleware.ThroughputControlMiddleware.GetThroughputStats(),
            cache = Middleware.ResponseCachingMiddleware.GetStatistics(),
            retry = Middleware.RequestRetryMiddleware.GetStatistics(),
            circuitBreaker = Middleware.GatewayProtectionMiddleware.GetCircuitStates(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Set global throughput limit.
    /// </summary>
    [HttpPost("throughput/limit")]
    public IActionResult SetThroughputLimit([FromBody] SetThroughputLimitRequest request)
    {
        if (request.Limit <= 0)
        {
            return BadRequest(new { error = "Limit must be greater than 0" });
        }

        Middleware.ThroughputControlMiddleware.SetGlobalThroughputLimit(request.Limit);

        return Ok(new
        {
            message = "Throughput limit updated",
            limit = request.Limit,
            timestamp = DateTime.UtcNow
        });
    }
}

public class SetThroughputLimitRequest
{
    public int Limit { get; set; }
}

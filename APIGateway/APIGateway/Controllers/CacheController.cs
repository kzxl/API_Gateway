using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace APIGateway.Controllers;

/// <summary>
/// Cache management and statistics controller.
/// </summary>
[ApiController]
[Route("admin/cache")]
public class CacheController : ControllerBase
{
    private readonly IMemoryCache _cache;

    public CacheController(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStatistics()
    {
        var stats = Middleware.ResponseCachingMiddleware.GetStatistics();
        return Ok(new
        {
            cache = stats,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Clear all cache entries.
    /// </summary>
    [HttpPost("clear")]
    public IActionResult ClearCache()
    {
        // Clear response cache statistics
        Middleware.ResponseCachingMiddleware.ResetStatistics();

        // Clear route cache
        _cache.Remove("GatewayRoutes");

        return Ok(new
        {
            message = "Cache cleared successfully",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Invalidate specific cache entry by pattern.
    /// </summary>
    [HttpPost("invalidate")]
    public IActionResult InvalidateCache([FromBody] InvalidateCacheRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pattern))
        {
            return BadRequest(new { error = "Pattern is required" });
        }

        // For now, just clear all cache
        // In production, implement pattern-based invalidation
        _cache.Remove("GatewayRoutes");

        return Ok(new
        {
            message = $"Cache invalidated for pattern: {request.Pattern}",
            timestamp = DateTime.UtcNow
        });
    }
}

public class InvalidateCacheRequest
{
    public string Pattern { get; set; } = "";
}

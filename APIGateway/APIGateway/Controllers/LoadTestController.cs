using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace APIGateway.Controllers;

/// <summary>
/// Load testing endpoint for performance benchmarking.
/// UArch: Minimal overhead for accurate throughput measurement.
/// </summary>
[ApiController]
[Route("test")]
public class LoadTestController : ControllerBase
{
    private static long _requestCount = 0;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Simple echo endpoint - minimal processing overhead.
    /// Use this to test raw gateway throughput.
    /// </summary>
    [HttpGet("echo")]
    public IActionResult Echo()
    {
        Interlocked.Increment(ref _requestCount);
        return Ok(new
        {
            message = "pong",
            timestamp = DateTime.UtcNow,
            requestId = Guid.NewGuid().ToString("N")[..8]
        });
    }

    /// <summary>
    /// Echo with payload - test serialization overhead.
    /// </summary>
    [HttpPost("echo")]
    public IActionResult EchoPost([FromBody] object payload)
    {
        Interlocked.Increment(ref _requestCount);
        return Ok(new
        {
            message = "received",
            payload,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Simulate backend latency.
    /// Query param: ?delay=100 (milliseconds)
    /// </summary>
    [HttpGet("delay")]
    public async Task<IActionResult> Delay([FromQuery] int delay = 100)
    {
        Interlocked.Increment(ref _requestCount);
        await Task.Delay(delay);
        return Ok(new
        {
            message = "delayed",
            delayMs = delay,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// CPU-intensive endpoint - test CPU-bound performance.
    /// </summary>
    [HttpGet("cpu")]
    public IActionResult CpuIntensive([FromQuery] int iterations = 10000)
    {
        Interlocked.Increment(ref _requestCount);

        var sw = Stopwatch.StartNew();
        double result = 0;
        for (int i = 0; i < iterations; i++)
        {
            result += Math.Sqrt(i) * Math.Sin(i);
        }
        sw.Stop();

        return Ok(new
        {
            message = "computed",
            iterations,
            result,
            elapsedMs = sw.ElapsedMilliseconds,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Memory allocation test - test GC pressure.
    /// </summary>
    [HttpGet("memory")]
    public IActionResult MemoryTest([FromQuery] int size = 1000)
    {
        Interlocked.Increment(ref _requestCount);

        var data = new byte[size * 1024]; // size in KB
        new Random().NextBytes(data);

        return Ok(new
        {
            message = "allocated",
            sizeKb = size,
            hash = data.Take(100).Sum(b => b),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get current load test statistics.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult Stats()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var count = Interlocked.Read(ref _requestCount);
        var avgReqPerSec = count / uptime.TotalSeconds;

        return Ok(new
        {
            totalRequests = count,
            uptimeSeconds = uptime.TotalSeconds,
            avgRequestsPerSecond = Math.Round(avgReqPerSec, 2),
            startTime = _startTime,
            currentTime = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Reset statistics counter.
    /// </summary>
    [HttpPost("stats/reset")]
    public IActionResult ResetStats()
    {
        Interlocked.Exchange(ref _requestCount, 0);
        return Ok(new { message = "Statistics reset" });
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }
}

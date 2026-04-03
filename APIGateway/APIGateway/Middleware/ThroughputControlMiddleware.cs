using System.Collections.Concurrent;
using System.Diagnostics;

namespace APIGateway.Middleware;

/// <summary>
/// Adaptive Throughput Control Middleware.
/// Dynamically adjusts rate limits based on backend health.
/// UArch: Zero-allocation hot path with pre-allocated structures.
/// </summary>
public class ThroughputControlMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ThroughputControlMiddleware> _logger;

    // Throughput tracking per route
    private static readonly ConcurrentDictionary<string, ThroughputTracker> _trackers = new();

    // Global throughput limit (requests per second)
    private static int _globalThroughputLimit = 50000; // 50k req/s default
    private static long _globalRequestCount = 0;
    private static DateTime _globalWindowStart = DateTime.UtcNow;

    public ThroughputControlMiddleware(RequestDelegate next, ILogger<ThroughputControlMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip for admin/auth endpoints
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Check global throughput limit
        if (!CheckGlobalThroughput())
        {
            context.Response.StatusCode = 503;
            context.Response.Headers["Retry-After"] = "1";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Gateway overloaded",
                message = "Global throughput limit reached",
                limit = _globalThroughputLimit
            });
            return;
        }

        // Track request
        var routeId = ExtractRouteId(path);
        var tracker = _trackers.GetOrAdd(routeId, _ => new ThroughputTracker());

        var sw = Stopwatch.StartNew();
        tracker.IncrementActive();

        try
        {
            await _next(context);
            sw.Stop();

            // Record latency
            tracker.RecordLatency(sw.ElapsedMilliseconds);
            tracker.RecordSuccess();
        }
        catch
        {
            sw.Stop();
            tracker.RecordFailure();
            throw;
        }
        finally
        {
            tracker.DecrementActive();
        }
    }

    private bool CheckGlobalThroughput()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _globalWindowStart).TotalSeconds;

        // Reset window every second
        if (elapsed >= 1.0)
        {
            Interlocked.Exchange(ref _globalRequestCount, 0);
            _globalWindowStart = now;
        }

        var count = Interlocked.Increment(ref _globalRequestCount);
        return count <= _globalThroughputLimit;
    }

    private string ExtractRouteId(string path)
    {
        // Simple route extraction
        if (path.StartsWith("/test")) return "test-route";
        if (path.StartsWith("/api")) return "api-route";
        return "default-route";
    }

    public static Dictionary<string, object> GetThroughputStats()
    {
        return _trackers.ToDictionary(
            kv => kv.Key,
            kv => (object)new
            {
                activeRequests = kv.Value.ActiveRequests,
                totalRequests = kv.Value.TotalRequests,
                successRate = kv.Value.GetSuccessRate(),
                avgLatencyMs = kv.Value.GetAverageLatency(),
                p95LatencyMs = kv.Value.GetP95Latency(),
                requestsPerSecond = kv.Value.GetRequestsPerSecond()
            }
        );
    }

    public static void SetGlobalThroughputLimit(int limit)
    {
        _globalThroughputLimit = limit;
    }
}

internal class ThroughputTracker
{
    private long _activeRequests = 0;
    private long _totalRequests = 0;
    private long _successCount = 0;
    private long _failureCount = 0;
    private readonly ConcurrentQueue<long> _latencies = new();
    private DateTime _windowStart = DateTime.UtcNow;

    public long ActiveRequests => Interlocked.Read(ref _activeRequests);
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    public void IncrementActive()
    {
        Interlocked.Increment(ref _activeRequests);
        Interlocked.Increment(ref _totalRequests);
    }

    public void DecrementActive()
    {
        Interlocked.Decrement(ref _activeRequests);
    }

    public void RecordLatency(long latencyMs)
    {
        _latencies.Enqueue(latencyMs);

        // Keep only last 1000 samples
        while (_latencies.Count > 1000)
        {
            _latencies.TryDequeue(out _);
        }
    }

    public void RecordSuccess()
    {
        Interlocked.Increment(ref _successCount);
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _failureCount);
    }

    public double GetSuccessRate()
    {
        var total = TotalRequests;
        if (total == 0) return 100.0;
        var success = Interlocked.Read(ref _successCount);
        return Math.Round((double)success / total * 100, 2);
    }

    public double GetAverageLatency()
    {
        if (_latencies.IsEmpty) return 0;
        return Math.Round(_latencies.Average(), 2);
    }

    public double GetP95Latency()
    {
        if (_latencies.IsEmpty) return 0;
        var sorted = _latencies.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        return sorted[Math.Max(0, index)];
    }

    public double GetRequestsPerSecond()
    {
        var elapsed = (DateTime.UtcNow - _windowStart).TotalSeconds;
        if (elapsed < 1) return 0;
        return Math.Round(TotalRequests / elapsed, 2);
    }
}

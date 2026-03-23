using System.Collections.Concurrent;
using System.Diagnostics;

namespace APIGateway.Middleware;

/// <summary>
/// Tracks per-route metrics: request count, latency, errors, throughput.
/// Only tracks proxy traffic (not admin endpoints).
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, RouteMetrics> _metrics = new();

    public MetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only track proxy traffic, skip admin/auth endpoints
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
            sw.Stop();

            var routeKey = GetRouteKey(context);
            RecordRequest(routeKey, sw.ElapsedMilliseconds, context.Response.StatusCode < 400);
        }
        catch (Exception)
        {
            sw.Stop();
            var routeKey = GetRouteKey(context);
            RecordRequest(routeKey, sw.ElapsedMilliseconds, false);
            throw;
        }
    }

    private static string GetRouteKey(HttpContext context)
    {
        // Try to get YARP route ID from endpoint metadata
        var endpoint = context.GetEndpoint();
        var routeId = endpoint?.Metadata?.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>()?.Config?.RouteId;
        return routeId ?? $"{context.Request.Method} {context.Request.Path}";
    }

    private static void RecordRequest(string routeKey, long latencyMs, bool success)
    {
        var metrics = _metrics.GetOrAdd(routeKey, _ => new RouteMetrics());
        metrics.Record(latencyMs, success);
    }

    public static Dictionary<string, object> GetAllMetrics()
    {
        return _metrics.ToDictionary(
            kv => kv.Key,
            kv => (object)kv.Value.GetSnapshot());
    }

    public static void Reset()
    {
        _metrics.Clear();
    }
}

public class RouteMetrics
{
    private long _totalRequests;
    private long _successCount;
    private long _errorCount;
    private long _totalLatencyMs;
    private long _maxLatencyMs;
    private long _minLatencyMs = long.MaxValue;
    private readonly DateTime _startTime = DateTime.UtcNow;

    // Sliding window for throughput (last 60 seconds)
    private readonly ConcurrentQueue<DateTime> _recentRequests = new();

    public void Record(long latencyMs, bool success)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);

        if (success)
            Interlocked.Increment(ref _successCount);
        else
            Interlocked.Increment(ref _errorCount);

        // Update max
        long currentMax;
        do { currentMax = Interlocked.Read(ref _maxLatencyMs); }
        while (latencyMs > currentMax && Interlocked.CompareExchange(ref _maxLatencyMs, latencyMs, currentMax) != currentMax);

        // Update min
        long currentMin;
        do { currentMin = Interlocked.Read(ref _minLatencyMs); }
        while (latencyMs < currentMin && Interlocked.CompareExchange(ref _minLatencyMs, latencyMs, currentMin) != currentMin);

        // Track for throughput
        _recentRequests.Enqueue(DateTime.UtcNow);

        // Cleanup old entries (older than 60s)
        while (_recentRequests.TryPeek(out var oldest) && (DateTime.UtcNow - oldest).TotalSeconds > 60)
            _recentRequests.TryDequeue(out _);
    }

    public object GetSnapshot()
    {
        var total = Interlocked.Read(ref _totalRequests);
        var success = Interlocked.Read(ref _successCount);
        var errors = Interlocked.Read(ref _errorCount);
        var totalLatency = Interlocked.Read(ref _totalLatencyMs);
        var maxLat = Interlocked.Read(ref _maxLatencyMs);
        var minLat = Interlocked.Read(ref _minLatencyMs);
        if (minLat == long.MaxValue) minLat = 0;

        // Count requests in last 60s for throughput
        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        var recentCount = _recentRequests.Count(r => r > cutoff);
        var uptimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds;

        return new
        {
            totalRequests = total,
            successCount = success,
            errorCount = errors,
            errorRate = total > 0 ? Math.Round((double)errors / total * 100, 2) : 0,
            avgLatencyMs = total > 0 ? Math.Round((double)totalLatency / total, 2) : 0,
            maxLatencyMs = maxLat,
            minLatencyMs = minLat,
            throughputPerMinute = recentCount,
            throughputPerSecond = Math.Round(recentCount / 60.0, 2),
            uptimeSeconds = Math.Round(uptimeSeconds, 0)
        };
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Middleware;

/// <summary>
/// Combined middleware: Rate Limiting + IP Filter + Circuit Breaker + Request Logging.
/// Runs BEFORE proxy, handles all protection logic in a single pass.
/// </summary>
public class GatewayProtectionMiddleware
{
    private readonly RequestDelegate _next;

    // ── Rate Limiting: per routeId per IP, sliding window ──
    private static readonly ConcurrentDictionary<string, RateLimitBucket> _rateLimits = new();

    // ── Circuit Breaker: per routeId ──
    private static readonly ConcurrentDictionary<string, CircuitState> _circuits = new();

    // ── Logging queue (background flush) ──
    private static readonly ConcurrentQueue<RequestLog> _logQueue = new();
    private static Timer? _flushTimer;
    private static IServiceScopeFactory? _scopeFactory;

    public GatewayProtectionMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory ??= scopeFactory;
        _flushTimer ??= new Timer(FlushLogs, null, 3000, 3000);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Resolve route config from DB
        Models.Route? routeConfig = null;
        using (var scope = _scopeFactory!.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            var routes = await db.Routes.ToListAsync();
            // Find best matching route
            routeConfig = routes.FirstOrDefault(r =>
                path.StartsWith(r.MatchPath.Replace("/{**catch-all}", "").Replace("{**catch-all}", ""))
                || r.MatchPath == "/{**catch-all}");
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var routeId = routeConfig?.RouteId ?? "unknown";

        // ── 1. IP Filter ──
        if (routeConfig != null)
        {
            if (!string.IsNullOrWhiteSpace(routeConfig.IpBlacklist))
            {
                var blocked = routeConfig.IpBlacklist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());
                if (blocked.Any(ip => clientIp.Contains(ip)))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "IP blocked" });
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(routeConfig.IpWhitelist))
            {
                var allowed = routeConfig.IpWhitelist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());
                if (!allowed.Any(ip => clientIp.Contains(ip)))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "IP not allowed" });
                    return;
                }
            }
        }

        // ── 2. Rate Limiting ──
        if (routeConfig != null && routeConfig.RateLimitPerSecond > 0)
        {
            var key = $"{routeId}:{clientIp}";
            var bucket = _rateLimits.GetOrAdd(key, _ => new RateLimitBucket());
            if (!bucket.TryConsume(routeConfig.RateLimitPerSecond))
            {
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = "1";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    retryAfter = 1,
                    limit = routeConfig.RateLimitPerSecond
                });
                return;
            }
        }

        // ── 3. Circuit Breaker ──
        if (routeConfig != null && routeConfig.CircuitBreakerThreshold > 0)
        {
            var circuit = _circuits.GetOrAdd(routeId, _ => new CircuitState());
            if (circuit.IsOpen(routeConfig.CircuitBreakerDurationSeconds))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Service unavailable (circuit breaker open)",
                    retryAfter = routeConfig.CircuitBreakerDurationSeconds
                });
                return;
            }
        }

        // ── Execute request ──
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
            sw.Stop();

            // Record circuit breaker success
            if (routeConfig != null && routeConfig.CircuitBreakerThreshold > 0)
            {
                var circuit = _circuits.GetOrAdd(routeId, _ => new CircuitState());
                circuit.RecordResult(context.Response.StatusCode < 500);

                if (circuit.GetErrorRate() >= routeConfig.CircuitBreakerThreshold)
                    circuit.Trip();
            }
        }
        catch (Exception)
        {
            sw.Stop();
            if (routeConfig != null && routeConfig.CircuitBreakerThreshold > 0)
            {
                var circuit = _circuits.GetOrAdd(routeId, _ => new CircuitState());
                circuit.RecordResult(false);
                if (circuit.GetErrorRate() >= routeConfig.CircuitBreakerThreshold)
                    circuit.Trip();
            }
            throw;
        }

        // ── 4. Log request (async queue) ──
        _logQueue.Enqueue(new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = path,
            StatusCode = context.Response.StatusCode,
            LatencyMs = sw.ElapsedMilliseconds,
            ClientIp = clientIp,
            RouteId = routeId,
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault()
        });
    }

    private static void FlushLogs(object? state)
    {
        if (_logQueue.IsEmpty || _scopeFactory == null) return;
        var logs = new List<RequestLog>();
        while (_logQueue.TryDequeue(out var log) && logs.Count < 100)
            logs.Add(log);

        if (logs.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            db.RequestLogs.AddRange(logs);
            db.SaveChanges();
        }
        catch { /* log flush failure - non-critical */ }
    }

    // ── Circuit Breaker state tracking ──
    public static Dictionary<string, object> GetCircuitStates()
    {
        return _circuits.ToDictionary(kv => kv.Key, kv => (object)new
        {
            state = kv.Value.IsOpen(30) ? "OPEN" : "CLOSED",
            errorRate = kv.Value.GetErrorRate(),
            totalRequests = kv.Value.TotalRequests
        });
    }
}

internal class RateLimitBucket
{
    private readonly ConcurrentQueue<DateTime> _timestamps = new();

    public bool TryConsume(int maxPerSecond)
    {
        var now = DateTime.UtcNow;
        // Clean old entries
        while (_timestamps.TryPeek(out var oldest) && (now - oldest).TotalSeconds > 1)
            _timestamps.TryDequeue(out _);

        if (_timestamps.Count >= maxPerSecond)
            return false;

        _timestamps.Enqueue(now);
        return true;
    }
}

internal class CircuitState
{
    private long _success;
    private long _failure;
    private DateTime? _trippedAt;
    public long TotalRequests => Interlocked.Read(ref _success) + Interlocked.Read(ref _failure);

    public void RecordResult(bool success)
    {
        if (success) Interlocked.Increment(ref _success);
        else Interlocked.Increment(ref _failure);
    }

    public double GetErrorRate()
    {
        var total = TotalRequests;
        if (total < 5) return 0; // Need minimum samples
        return Math.Round((double)Interlocked.Read(ref _failure) / total * 100, 2);
    }

    public void Trip() => _trippedAt = DateTime.UtcNow;

    public bool IsOpen(int durationSeconds)
    {
        if (_trippedAt == null) return false;
        if ((DateTime.UtcNow - _trippedAt.Value).TotalSeconds > durationSeconds)
        {
            _trippedAt = null; // Auto-reset
            Interlocked.Exchange(ref _success, 0);
            Interlocked.Exchange(ref _failure, 0);
            return false;
        }
        return true;
    }
}

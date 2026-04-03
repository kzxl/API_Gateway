using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace APIGateway.Middleware;

/// <summary>
/// Combined middleware: Rate Limiting + IP Filter + Circuit Breaker + Request Logging.
/// Integrates with GoFlow Sidecar Engine for extreme performance.
/// </summary>
public class GatewayProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    // GoFlow Engine Client
    private static readonly HttpClient _goFlowClient = new() { BaseAddress = new Uri("http://127.0.0.1:50051") };

    // ── Pre-allocated States ──
    private static readonly ConcurrentDictionary<string, CircuitState> _circuits = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _rateLimiters = new();
    private static readonly ConcurrentQueue<RequestLog> _logQueue = new();
    private static Timer? _flushTimer;
    private static Timer? _syncTimer;
    private static long _lastRoutesVersion = 0;

    public GatewayProtectionMiddleware(RequestDelegate next, IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _cache = cache;
        _scopeFactory = scopeFactory;
        
        // Start background flusher for logs to Database (Batching)
        _flushTimer ??= new Timer(FlushLogsToDb, _scopeFactory, 3000, 3000);

        // L1-L2 Hybrid Sync: Poll routes version every 1 second from GoCache L2
        if (_syncTimer == null)
        {
            _syncTimer = new Timer(SyncRoutesVersion, null, 1000, 1000);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // ── 1. Resolve route config from Memory Cache (No DB hit on pipeline) ──
        if (!_cache.TryGetValue("GatewayRoutes", out List<Models.Route>? routes) || routes == null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
                routes = await db.Routes.AsNoTracking().ToListAsync();
                _cache.Set("GatewayRoutes", routes, TimeSpan.FromDays(365)); // Cache forever, invalidated by Version Bump Sync
            }
            catch { routes = new List<Models.Route>(); }
        }

        // Find best matching route without allocating new strings
        var routeConfig = routes!.FirstOrDefault(r =>
        {
            if (r.MatchPath == "/{**catch-all}") return true;
            
            var matchPath = r.MatchPath.AsSpan();
            if (matchPath.EndsWith("/{**catch-all}"))
                matchPath = matchPath[..^15];
            else if (matchPath.EndsWith("{**catch-all}"))
                matchPath = matchPath[..^14];

            return path.AsSpan().StartsWith(matchPath);
        });

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var routeId = routeConfig?.RouteId ?? "unknown";

        // ── 2. IP Filter ──
        if (routeConfig != null)
        {
            if (!string.IsNullOrWhiteSpace(routeConfig.IpBlacklist))
            {
                if (routeConfig.IpBlacklist.Contains(clientIp))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "IP blocked" });
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(routeConfig.IpWhitelist))
            {
                if (!routeConfig.IpWhitelist.Contains(clientIp))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "IP not allowed" });
                    return;
                }
            }
        }

        // ── 3. Rate Limiting (In-Memory .NET 8 TokenBucket) ──
        // Extremely fast nano-second check, removes HTTP overhead per request!
        if (routeConfig != null && routeConfig.RateLimitPerSecond > 0)
        {
            var key = $"{routeId}:{clientIp}";
            var limiter = _rateLimiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = routeConfig.RateLimitPerSecond,
                TokensPerPeriod = routeConfig.RateLimitPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true
            }));

            using var lease = limiter.AttemptAcquire(1);
            if (!lease.IsAcquired)
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

        // ── 4. Circuit Breaker ──
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
            EnqueueLog(context, path, 500, sw.ElapsedMilliseconds, clientIp, routeId);
            throw;
        }

        // ── 5. Log request (Enqueue for GoFlow Batching) ──
        EnqueueLog(context, path, context.Response.StatusCode, sw.ElapsedMilliseconds, clientIp, routeId);
    }

    private void EnqueueLog(HttpContext context, string path, int statusCode, long latencyMs, string clientIp, string routeId)
    {
        _logQueue.Enqueue(new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = path,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            ClientIp = clientIp,
            RouteId = routeId,
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? ""
        });
    }

    private static void FlushLogsToDb(object? state)
    {
        if (_logQueue.IsEmpty || state is not IServiceScopeFactory scopeFactory) return;

        var logs = new List<RequestLog>();
        while (_logQueue.TryDequeue(out var log) && logs.Count < 5000)
        {
            logs.Add(log);
        }

        if (logs.Count > 0)
        {
            // Bulk insert to Database
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
                    db.RequestLogs.AddRange(logs);
                    await db.SaveChangesAsync();
                }
                catch { /* Ignore */ }
            });
        }
    }

    // ── Pre-allocated States and Sync Logic ──

    private void SyncRoutesVersion(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await _goFlowClient.GetFromJsonAsync<GoCacheVersion>("/cache/routes_version");
                if (response != null && response.Version > _lastRoutesVersion)
                {
                    if (_lastRoutesVersion > 0) // Only remove cache if it's an actual update (not first pull)
                    {
                        _cache.Remove("GatewayRoutes");
                    }
                    _lastRoutesVersion = response.Version;
                }
            }
            catch { /* Ignore */ }
        });
    }

    private class GoCacheVersion
    {
        public long Version { get; set; }
    }

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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
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

    // ── Circuit Breaker: per routeId ──
    private static readonly ConcurrentDictionary<string, CircuitState> _circuits = new();

    public GatewayProtectionMiddleware(RequestDelegate next, IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/admin") || path.StartsWith("/auth") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // ── 1. Resolve route config from Memory Cache (No DB hit) ──
        if (!_cache.TryGetValue("GatewayRoutes", out List<Models.Route>? routes) || routes == null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            routes = await db.Routes.ToListAsync();
            _cache.Set("GatewayRoutes", routes, TimeSpan.FromMinutes(5));
        }

        // Find best matching route
        var routeConfig = routes.FirstOrDefault(r =>
            path.StartsWith(r.MatchPath.Replace("/{**catch-all}", "").Replace("{**catch-all}", ""))
            || r.MatchPath == "/{**catch-all}");

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

        // ── 3. Rate Limiting (via GoFlow Engine) ──
        if (routeConfig != null && routeConfig.RateLimitPerSecond > 0)
        {
            try
            {
                var response = await _goFlowClient.PostAsJsonAsync("/ratelimit", new
                {
                    routeId = routeId,
                    ip = clientIp,
                    limitPerSec = routeConfig.RateLimitPerSecond
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RateLimitResponse>();
                    if (result != null && !result.Allowed)
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
            }
            catch
            {
                // Fallback / fail-open if GoFlow is down
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
            // Send error log to GoFlow before throwing
            FireAndForgetLog(context, path, 500, sw.ElapsedMilliseconds, clientIp, routeId);
            throw;
        }

        // ── 5. Log request (via GoFlow Engine) ──
        FireAndForgetLog(context, path, context.Response.StatusCode, sw.ElapsedMilliseconds, clientIp, routeId);
    }

    private void FireAndForgetLog(HttpContext context, string path, int statusCode, long latencyMs, string clientIp, string routeId)
    {
        var logReq = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            method = context.Request.Method,
            path = path,
            statusCode = statusCode,
            latencyMs = latencyMs,
            clientIp = clientIp,
            routeId = routeId,
            userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? ""
        };

        // Fire and forget HTTP POST to GoFlow (no await)
        _ = Task.Run(async () =>
        {
            try
            {
                await _goFlowClient.PostAsJsonAsync("/log", logReq);
            }
            catch { /* Ignore logging errors */ }
        });
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

    private class RateLimitResponse
    {
        public bool Allowed { get; set; }
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

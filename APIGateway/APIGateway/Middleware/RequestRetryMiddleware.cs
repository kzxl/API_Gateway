using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Collections.Concurrent;

namespace APIGateway.Middleware;

/// <summary>
/// Request Retry Middleware with exponential backoff.
/// Uses Polly for resilience patterns.
/// UArch: Fire-and-forget for non-critical operations.
/// </summary>
public class RequestRetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestRetryMiddleware> _logger;

    // Retry policies per route (cached)
    private static readonly ConcurrentDictionary<string, AsyncRetryPolicy> _retryPolicies = new();

    // Statistics
    private static long _totalRetries = 0;
    private static long _successAfterRetry = 0;
    private static long _failedAfterRetry = 0;

    public RequestRetryMiddleware(RequestDelegate next, ILogger<RequestRetryMiddleware> logger)
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

        // Get or create retry policy
        var policy = GetRetryPolicy(context);

        if (policy == null)
        {
            // No retry configured
            await _next(context);
            return;
        }

        // Execute with retry
        await policy.ExecuteAsync(async () =>
        {
            await _next(context);

            // Check if response indicates transient failure
            if (IsTransientFailure(context.Response.StatusCode))
            {
                throw new TransientFailureException($"Transient failure: {context.Response.StatusCode}");
            }
        });
    }

    private AsyncRetryPolicy? GetRetryPolicy(HttpContext context)
    {
        // For now, use a default retry policy
        // In production, this should be configurable per route
        var routeId = context.Request.Path.Value ?? "default";

        return _retryPolicies.GetOrAdd(routeId, _ =>
        {
            return Policy
                .Handle<TransientFailureException>()
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                    {
                        // Exponential backoff: 100ms, 200ms, 400ms
                        var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt - 1) * 100);
                        return delay;
                    },
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        Interlocked.Increment(ref _totalRetries);
                        _logger.LogWarning(
                            "Retry {RetryCount} after {Delay}ms due to {Exception}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            exception.Message
                        );
                    }
                );
        });
    }

    private bool IsTransientFailure(int statusCode)
    {
        // Retry on these status codes
        return statusCode switch
        {
            408 => true, // Request Timeout
            429 => true, // Too Many Requests
            500 => true, // Internal Server Error
            502 => true, // Bad Gateway
            503 => true, // Service Unavailable
            504 => true, // Gateway Timeout
            _ => false
        };
    }

    public static RetryStatistics GetStatistics()
    {
        return new RetryStatistics
        {
            TotalRetries = Interlocked.Read(ref _totalRetries),
            SuccessAfterRetry = Interlocked.Read(ref _successAfterRetry),
            FailedAfterRetry = Interlocked.Read(ref _failedAfterRetry)
        };
    }
}

public class TransientFailureException : Exception
{
    public TransientFailureException(string message) : base(message) { }
}

public class RetryStatistics
{
    public long TotalRetries { get; set; }
    public long SuccessAfterRetry { get; set; }
    public long FailedAfterRetry { get; set; }
}

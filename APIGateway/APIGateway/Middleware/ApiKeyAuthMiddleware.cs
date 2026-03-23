namespace APIGateway.Middleware;

/// <summary>
/// Validates API key for admin endpoints (/admin/*).
/// Key is read from config "AdminAuth:ApiKey".
/// Pass via header: X-Api-Key
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _apiKey = config["AdminAuth:ApiKey"] ?? "gw-admin-key-change-me";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only protect /admin/* endpoints
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            // Allow Swagger/OPTIONS preflight through
            if (context.Request.Method == HttpMethods.Options)
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
                || providedKey.ToString() != _apiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
                return;
            }
        }

        await _next(context);
    }
}

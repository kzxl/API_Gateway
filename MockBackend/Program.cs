using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();

// Simple echo endpoint
app.MapGet("/test/echo", () => new
{
    message = "pong",
    timestamp = DateTime.UtcNow,
    requestId = Guid.NewGuid().ToString("N")[..8]
});

// Health check
app.MapGet("/test/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
});

// Delay endpoint
app.MapGet("/test/delay", async (int delay = 100) =>
{
    await Task.Delay(delay);
    return new
    {
        message = "delayed",
        delayMs = delay,
        timestamp = DateTime.UtcNow
    };
});

// CPU intensive
app.MapGet("/test/cpu", (int iterations = 10000) =>
{
    double result = 0;
    for (int i = 0; i < iterations; i++)
    {
        result += Math.Sqrt(i) * Math.Sin(i);
    }
    return new
    {
        message = "computed",
        iterations,
        result,
        timestamp = DateTime.UtcNow
    };
});

Console.WriteLine("Mock Backend Server running on http://localhost:5001");
Console.WriteLine("Endpoints:");
Console.WriteLine("  GET /test/echo");
Console.WriteLine("  GET /test/health");
Console.WriteLine("  GET /test/delay?delay=100");
Console.WriteLine("  GET /test/cpu?iterations=10000");

app.Run("http://localhost:5001");

using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;

namespace APIGateway.Controllers;

/// <summary>
/// WebSocket test controller for demo purposes.
/// </summary>
[ApiController]
[Route("ws")]
public class WebSocketController : ControllerBase
{
    private readonly ILogger<WebSocketController> _logger;
    private static long _activeConnections = 0;
    private static long _totalMessages = 0;

    public WebSocketController(ILogger<WebSocketController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// WebSocket echo endpoint for testing.
    /// </summary>
    [HttpGet("echo")]
    public async Task Echo()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        Interlocked.Increment(ref _activeConnections);
        _logger.LogInformation("WebSocket connection opened. Active: {Count}", _activeConnections);

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[4096];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    HttpContext.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                    break;
                }

                Interlocked.Increment(ref _totalMessages);

                // Echo back the message
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var response = $"Echo: {message} (msg #{_totalMessages})";
                var responseBytes = Encoding.UTF8.GetBytes(response);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(responseBytes),
                    WebSocketMessageType.Text,
                    true,
                    HttpContext.RequestAborted);

                _logger.LogDebug("WebSocket echo: {Message}", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error");
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
            _logger.LogInformation("WebSocket connection closed. Active: {Count}", _activeConnections);
        }
    }

    /// <summary>
    /// Get WebSocket statistics.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        return Ok(new
        {
            activeConnections = Interlocked.Read(ref _activeConnections),
            totalMessages = Interlocked.Read(ref _totalMessages),
            timestamp = DateTime.UtcNow
        });
    }
}

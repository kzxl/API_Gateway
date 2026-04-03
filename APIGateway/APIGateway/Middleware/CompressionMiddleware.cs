using System.IO.Compression;

namespace APIGateway.Middleware;

/// <summary>
/// Response Compression Middleware.
/// Supports Gzip and Brotli compression.
/// Performance: 60-80% bandwidth reduction for text responses.
/// </summary>
public class CompressionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CompressionMiddleware> _logger;

    // Minimum size to compress (bytes)
    private const int MinimumSizeToCompress = 1024; // 1KB

    // Compressible content types
    private static readonly HashSet<string> CompressibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "application/json",
        "application/xml",
        "text/xml",
        "application/x-javascript",
        "application/atom+xml",
        "application/rss+xml",
        "image/svg+xml"
    };

    public CompressionMiddleware(RequestDelegate next, ILogger<CompressionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if client accepts compression
        var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
        if (string.IsNullOrEmpty(acceptEncoding))
        {
            await _next(context);
            return;
        }

        // Determine compression algorithm (prefer Brotli over Gzip)
        string? compressionType = null;
        if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
        {
            compressionType = "br";
        }
        else if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            compressionType = "gzip";
        }

        if (compressionType == null)
        {
            await _next(context);
            return;
        }

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Check if response should be compressed
            if (ShouldCompress(context, responseBody))
            {
                responseBody.Seek(0, SeekOrigin.Begin);

                // Compress response
                context.Response.Headers.ContentEncoding = compressionType;
                context.Response.Headers.Remove("Content-Length");

                if (compressionType == "br")
                {
                    using var compressedStream = new BrotliStream(originalBodyStream, CompressionLevel.Fastest, leaveOpen: true);
                    await responseBody.CopyToAsync(compressedStream);
                }
                else
                {
                    using var compressedStream = new GZipStream(originalBodyStream, CompressionLevel.Fastest, leaveOpen: true);
                    await responseBody.CopyToAsync(compressedStream);
                }
            }
            else
            {
                // Don't compress - copy as-is
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private bool ShouldCompress(HttpContext context, MemoryStream responseBody)
    {
        // Don't compress if already compressed
        if (context.Response.Headers.ContainsKey("Content-Encoding"))
        {
            return false;
        }

        // Don't compress small responses
        if (responseBody.Length < MinimumSizeToCompress)
        {
            return false;
        }

        // Only compress successful responses
        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            return false;
        }

        // Check content type
        var contentType = context.Response.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // Remove charset if present
        var semicolonIndex = contentType.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            contentType = contentType[..semicolonIndex].Trim();
        }

        return CompressibleTypes.Contains(contentType);
    }
}

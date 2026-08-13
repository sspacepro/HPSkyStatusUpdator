using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;

        await _next(context);

        var elapsed =
            (DateTime.UtcNow - start).TotalMilliseconds;

        // Don't log successful requests.
        if (context.Response.StatusCode < 400)
            return;

        var user = context.Items["User"] as User;

        string username =
            user?.Username ?? "Unknown";

        var message =
            $"[{DateTime.Now:HH:mm:ss}] " +
            $"{username} " +
            $"{context.Request.Method} " +
            $"{context.Request.Path} -> " +
            $"{context.Response.StatusCode} " +
            $"({elapsed:F0}ms)";

        if (context.Response.StatusCode >= 500)
        {
            _logger.LogError(message);
        }
        else
        {
            _logger.LogWarning(message);
        }
    }
}
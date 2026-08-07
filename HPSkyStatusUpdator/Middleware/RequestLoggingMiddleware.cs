using HPSkyStatusUpdator.Models;
using HPSkyStatusUpdator.Services;

namespace HPSkyStatusUpdator.Middleware;



public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
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


        var user = context.Items["User"] as User;

        string username =
            user?.Username ?? "Unknown";


        _logger.LogInformation(
            $"[{DateTime.Now:HH:mm:ss}] " +
            $"{username} " +
            $"{context.Request.Method} " +
            $"{context.Request.Path} -> " +
            $"{context.Response.StatusCode} " +
            $"({elapsed}ms)"
        );
    }
}
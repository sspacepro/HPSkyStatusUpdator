using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
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


        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] " +
            $"{username} " +
            $"{context.Request.Method} " +
            $"{context.Request.Path} -> " +
            $"{context.Response.StatusCode} " +
            $"({elapsed}ms)"
        );
    }
}
using HPSkyStatusUpdator.Models;
using HPSkyStatusUpdator.Services;

namespace HPSkyStatusUpdator.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserService users)
    {
        // Public / separately authenticated endpoints.
        if (context.Request.Path.StartsWithSegments("/api/v1/register")
            || context.Request.Path.StartsWithSegments("/api/v1/health")
            || context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        User? user = users.Authenticate(context);

        if (user == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        context.Items["User"] = user;

        await _next(context);
    }
}

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
        // Don't authenticate the register endpoint.
        if (context.Request.Path.StartsWithSegments("/api/v1/register"))
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
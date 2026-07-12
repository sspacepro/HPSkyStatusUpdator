using HPSkyStatusUpdator.Services;

namespace HPSkyStatusUpdator.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }


    public async Task InvokeAsync(
        HttpContext context,
        RateLimitService limits,
        IConfiguration config)
    {
        var user = context.Items["User"] as Models.User;


        // If no user exists, authentication middleware should have handled it.
        if (user == null)
        {
            await _next(context);
            return;
        }


        int maxRequests =
            config.GetValue<int>("Settings:MaxRequestsPerMinute");


        if (!limits.Check(user.ClientId, maxRequests))
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync(
                "Too many requests"
            );
            return;
        }


        await _next(context);
    }
}
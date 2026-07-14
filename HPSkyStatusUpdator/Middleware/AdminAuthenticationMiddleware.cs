using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Services;

namespace HPSkyStatusUpdator.Middleware;

public class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        SettingsService settings)
    {
        if (!context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        string? adminKey =
            context.Request.Headers["Admin-Key"]
            .FirstOrDefault();

        string? storedKey =
            settings.GetString(SettingKeys.AdminKey);

        if (string.IsNullOrWhiteSpace(storedKey) ||
            adminKey != storedKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }
}
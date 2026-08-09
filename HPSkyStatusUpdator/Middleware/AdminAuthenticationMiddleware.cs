using System.Security.Cryptography;
using System.Text;
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

        if (string.IsNullOrWhiteSpace(storedKey)
            || string.IsNullOrWhiteSpace(adminKey)
            || !SecureEquals(adminKey, storedKey))
        string? configuredKey =
    Environment.GetEnvironmentVariable("ADMIN_KEY");
        //string? storedKey =
        //    settings.GetString(SettingKeys.AdminKey);

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrWhiteSpace(adminKey) ||
            !string.Equals(
                adminKey,
                configuredKey,
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }

    // Hash both sides so unequal lengths still compare in fixed time.
    private static bool SecureEquals(string provided, string expected)
    {
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}

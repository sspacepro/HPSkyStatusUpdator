using System.Security.Cryptography;
using System.Text;

namespace HPSkyStatusUpdator.Middleware;

public class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        bool isAdminEndpoint =
            context.Request.Path.StartsWithSegments("/api/admin");

        if (!isAdminEndpoint)
        {
            await _next(context);
            return;
        }

        // Admin endpoints are only available on port 81.
        if (context.Connection.LocalPort != 81)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Not Found");
            return;
        }

        string? adminKey =
            context.Request.Headers["Admin-Key"]
                .FirstOrDefault();

        string? configuredKey =
            Environment.GetEnvironmentVariable("ADMIN_KEY");

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrWhiteSpace(adminKey) ||
            !SecureEquals(adminKey, configuredKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }

    // Hash both values first so different-length keys still result
    // in a fixed-length constant-time comparison.
    private static bool SecureEquals(
        string provided,
        string expected)
    {
        byte[] providedHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(provided));

        byte[] expectedHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(
            providedHash,
            expectedHash);
    }
}
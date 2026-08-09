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
        // Only protect admin endpoints.
        // /api/v1/health is therefore unaffected.
        if (!context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        string? adminKey =
            context.Request.Headers["Admin-Key"]
                .FirstOrDefault();

        // Admin key comes from the environment (.env / Docker Compose)
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
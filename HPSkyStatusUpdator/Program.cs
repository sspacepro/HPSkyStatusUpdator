using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Middleware;
using HPSkyStatusUpdator.Models;
using HPSkyStatusUpdator.Services;
var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient<HypixelService>();

builder.Services.AddSingleton<HypixelService>();

builder.Services.AddHostedService<HypixelUpdater>();

builder.Services.AddSingleton<RegistrationService>();

builder.Services.AddSingleton<RateLimitService>();

builder.Services.AddSingleton<UserService>();

builder.Services.AddSingleton<DatabaseService>();

builder.Services.AddSingleton<SettingsService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    _ = scope.ServiceProvider.GetRequiredService<DatabaseService>();
}

var hypixel = app.Services.GetRequiredService<HypixelService>();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();







app.MapPost("/api/admin/settings/player-update-seconds/{seconds}",
(
    int seconds,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.PlayerUpdateSeconds,
        seconds
    );

    return Results.Ok();
});

app.MapGet("/api/admin/settings/player-update-seconds",
(
    SettingsService settings
) =>
{
    return Results.Ok(
        settings.GetInt(
            SettingKeys.PlayerUpdateSeconds,
            60
        )
    );
});






app.MapGet("/api/v1/status",
(
    HttpContext context,
    HypixelService hypixel
) =>
{
    var user = (User)context.Items["User"]!;
    return Results.Ok(new
    {
        username = user.Username,
        skyblockPlayers = hypixel.GetSkyblockPlayers()
    });
});
app.MapPost("/api/v1/register",
(
    HttpContext context,
    UserService users,
    RegistrationService registrations,
    RegisterRequest request
) =>
{
    string ip =
        context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";


    if (!registrations.CanRegister(ip))
    {
        return Results.StatusCode(429);
    }


    try
    {
        var user = users.Register(
            request.Username,
            ip
        );


        return Results.Ok(new
        {
            username = user.Username,
            clientId = user.ClientId
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message
        });
    }
});
app.MapPost("/api/admin/settings/{key}",
(
    string key,
    string value,
    SettingsService settings
) =>
{
    settings.Set(key, value);
    return Results.Ok();
});

app.MapGet("/api/admin/settings/{key}",
(
    string key,
    SettingsService settings
) =>
{
    var value = settings.Get(key);

    if (value == null)
        return Results.NotFound();

    return Results.Ok(value);
});

app.Run();
record RegisterRequest(string Username);


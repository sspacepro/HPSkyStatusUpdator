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

builder.Services.AddHostedService<PlayerWatcherService>();

builder.Services.AddHttpClient<HypixelPlayerService>();

builder.Services.AddSingleton<NotificationService>();

builder.Services.AddHttpClient<AuctionService>();

builder.Services.AddSingleton<AuctionService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    _ = scope.ServiceProvider.GetRequiredService<DatabaseService>();
}

var hypixel = app.Services.GetRequiredService<HypixelService>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<AdminAuthenticationMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();


app.MapGet("/api/admin/auction/{itemTag}",
async (
    string itemTag,
    AuctionService auctions
) =>
{
    var watch = new AuctionWatch
    {
        ItemTag = itemTag
    };

    var result = await auctions.GetLowestBin(watch);

    if (result == null)
        return Results.NotFound("Item not found.");

    return Results.Ok(result);
});



app.MapPost("/api/admin/settings/hypixel-update-interval-seconds/{seconds}",
(
    int seconds,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.HypixelUpdateIntervalSeconds,
        seconds
    );

    return Results.Ok();
});

app.MapGet("/api/admin/settings/hypixel-update-interval-seconds",
(
    SettingsService settings
) =>
{
    return Results.Ok(
        settings.GetInt(
            SettingKeys.HypixelUpdateIntervalSeconds,
            60
        )
    );
});


app.MapGet("/api/v1/playerstatus",
(
    HttpContext context,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    return Results.Ok(
        users.GetPlayerStatuses(user.ClientId)
    );
});
app.MapDelete("/api/v1/watch/{username}",
(
    HttpContext context,
    string username,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    if (!users.RemoveWatchPlayer(
        user.ClientId,
        username))
    {
        return Results.NotFound(
            "Player is not being watched."
        );
    }

    return Results.Ok();
});
app.MapGet("/api/v1/watch",
(
    HttpContext context,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    return Results.Ok(
        users.GetWatchList(user.ClientId)
    );
});
app.MapGet("/api/v1/notifications",
(
    HttpContext context,
    NotificationService notifications
) =>
{
    var user = (User)context.Items["User"]!;

    var result = notifications.Get(user.ClientId);

    return Results.Ok(result);
});
app.MapPost("/api/v1/watch/{username}",
async (
    HttpContext context,
    string username,
    UserService users,
    HypixelPlayerService hypixelPlayers
) =>
{
    var user = (User)context.Items["User"]!;

    try
    {
        if (!await users.AddWatchPlayer(
            user.ClientId,
            username,
            hypixelPlayers))
        {
            return Results.BadRequest(
    "Player is already being watched or your watch list is full."
);
        }

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});
app.MapGet("/api/admin/stats",
(
    UserService users,
    HypixelService hypixel
) =>
{
    return Results.Ok(new
    {
        RegisteredUsers = users.GetUserCount(),
        SkyBlockPlayers = hypixel.GetSkyblockPlayers(),
        ServerTime = DateTime.UtcNow
    });
});
app.MapPost("/api/admin/users/{username}/block",
(
    string username,
    UserService users
) =>
{
    if (!users.SetBlocked(username, true))
        return Results.NotFound();

    return Results.Ok();
});

app.MapPost("/api/admin/users/{username}/unblock",
(
    string username,
    UserService users
) =>
{
    if (!users.SetBlocked(username, false))
        return Results.NotFound();

    return Results.Ok();
});
app.MapGet("/api/admin/users",
(
    UserService users
) =>
{
    return Results.Ok(
        users.GetAllUsers()
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
app.MapGet("/api/v1/watch/status",
(
    HttpContext context,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    return Results.Ok(
        users.GetPlayerStatuses(user.ClientId)
    );
});
app.MapPost("/api/admin/shutdown",
(
    IHostApplicationLifetime lifetime
) =>
{
    Task.Run(() =>
    {
        Thread.Sleep(5000);
        lifetime.StopApplication();
    });

    return Results.Ok("Server shutting down.");
});
app.MapPost("/api/admin/settings/{key}",
(
    string key,
    string value,
    SettingsService settings
) =>
{
    if (key == "AdminKey")
    {
        return Results.BadRequest(
            "Cannot modify AdminKey through API."
        );
    }
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


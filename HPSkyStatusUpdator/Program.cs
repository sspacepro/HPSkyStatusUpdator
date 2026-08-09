using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Middleware;
using HPSkyStatusUpdator.Models;
using HPSkyStatusUpdator.Services;
using Microsoft.Extensions.Logging;
using System.IO;


DateTime serverStartTime = DateTime.UtcNow;
var builder = WebApplication.CreateBuilder(args);


builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Shared factory so singleton services can hold state without capturing
// a typed-client transient registration incorrectly.
builder.Services.AddHttpClient();
builder.Services.AddHttpClient(nameof(HypixelService));
builder.Services.AddHttpClient(nameof(AuctionService));
builder.Services.AddHttpClient(nameof(ItemCacheService));

builder.Services.AddSingleton<HypixelService>(sp =>
    new HypixelService(
        sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(HypixelService)),
        sp.GetRequiredService<SettingsService>(),
        sp.GetRequiredService<ILogger<HypixelService>>()));

builder.Services.AddHostedService<HypixelUpdater>();

builder.Services.AddHostedService<AuctionCacheService>();

builder.Services.AddSingleton<RegistrationService>();

builder.Services.AddSingleton<RateLimitService>();

builder.Services.AddSingleton<UserService>();

builder.Services.AddSingleton<DatabaseService>();

builder.Services.AddSingleton<SettingsService>();

//builder.Services.AddHostedService<PlayerWatcherService>();

//builder.Services.AddHttpClient<HypixelPlayerService>();

builder.Services.AddSingleton<NotificationService>();

builder.Services.AddSingleton<AuctionService>(sp =>
    new AuctionService(
        sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(AuctionService)),
        sp.GetRequiredService<ILogger<AuctionService>>()));

//builder.Services.AddHostedService<PlayerWatcherService>();

builder.Services.AddHostedService<AuctionWatcherService>();

builder.Services.AddSingleton<ServiceHealthService>();

builder.Services.AddSingleton<ItemCacheService>(sp =>
    new ItemCacheService(
        sp.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(ItemCacheService)),
        sp.GetRequiredService<SettingsService>(),
        sp.GetRequiredService<AuctionService>(),
        sp.GetRequiredService<DatabaseService>(),
        sp.GetRequiredService<ServiceHealthService>(),
        sp.GetRequiredService<ILogger<ItemCacheService>>()));

builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<ItemCacheService>());

builder.Services.AddSingleton<HealthService>();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddSingleton<DatabaseBackupService>();

builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<DatabaseBackupService>());
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

/*
var logFile = new StreamWriter("console.log", append: true)
{
    AutoFlush = true
};

Console.SetOut(new MultiTextWriter(
    Console.Out,
    logFile
));
*/

app.MapPost(
    "/api/admin/backup",
    (DatabaseBackupService backupService) =>
    {
        backupService.BackupDatabase();

        return Results.Ok(new
        {
            Message = "Database backup created."
        });
    });
app.MapGet(
"/api/admin/status",
(
    HypixelService hypixel,
    AuctionService auctions,
    ItemCacheService items,
    NotificationService notifications,
    UserService users
) =>
{
    return Results.Ok(new
    {
        Status = "Online",

        UptimeSeconds =
            (long)(DateTime.UtcNow - serverStartTime)
            .TotalSeconds,

        Users =
            users.GetAllUsers().Count,

        SkyBlockPlayers =
            hypixel.GetSkyblockPlayers(),

        HypixelOnline =
            hypixel.GetSkyblockPlayers() >= 0,

        CachedAuctions =
            auctions.GetAllAuctions().Count,

        CachedItems =
            items.GetItems().Count,

        QueuedNotifications =
            notifications.GetQueuedNotificationCount(),

        Time = DateTime.UtcNow
    });
});
app.MapGet(
"/api/v1/health",
(
    HypixelService hypixel,
    AuctionService auctions,
    ItemCacheService items,
    NotificationService notifications,
    ServiceHealthService health
) =>
{
    bool healthy =
     auctions.GetAuctionCount() > 0
    && items.GetItemCount() > 0;
    return Results.Ok(new
    {
        Status = healthy
    ? "Healthy"
    : "Degraded",

        UptimeSeconds =
            (long)(DateTime.UtcNow - serverStartTime)
            .TotalSeconds,

        HypixelOnline =
            hypixel.GetSkyblockPlayers() >= 0,

        SkyBlockPlayers =
            hypixel.GetSkyblockPlayers(),

        CachedAuctions =
            auctions.GetAuctionCount(),

        CachedItems =
            items.GetItemCount(),

        QueuedNotifications =
            notifications.GetQueuedNotificationCount(),

        Services =
            health.GetStatus()


    });
});

app.MapGet("/api/v1/items",
(
    ItemCacheService itemCache
) =>
{
    return Results.Ok(
        itemCache.GetItems()
    );
});

app.MapPost("/api/admin/settings/item-cache-update-minutes/{minutes}",
(
    int minutes,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.ItemCacheUpdateMinutes,
        minutes
    );

    return Results.Ok();
});

app.MapGet("/api/admin/settings/item-cache-update-minutes",
(
    SettingsService settings
) =>
{
    int minutes = settings.GetInt(
        SettingKeys.ItemCacheUpdateMinutes,
        1440
    );

    return Results.Ok(minutes);
});


app.MapPost("/api/admin/settings/watch-cleanup-interval-minutes/{minutes}",
(
    int minutes,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.WatchCleanupIntervalMinutes,
        minutes
    );

    return Results.Ok();
});

app.MapGet("/api/admin/settings/watch-cleanup-interval-minutes",
(
    SettingsService settings
) =>
{

    return Results.Ok(
    settings.GetInt(
        SettingKeys.WatchCleanupIntervalMinutes,
        60
    )
);
});

app.MapPost("/api/admin/settings/watch-expiration-days/{days}",
(
    int days,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.WatchExpirationDays,
        days
    );

    return Results.Ok();
});

app.MapGet("/api/admin/settings/watch-expiration-days",
(
    SettingsService settings
) =>
{

    return Results.Ok(
    settings.GetInt(
        SettingKeys.WatchExpirationDays,
        30
    )
);

});

app.MapGet("/api/admin/settings/auction-cache-refresh",
(
    SettingsService settings
) =>
{


    return Results.Ok(
    settings.GetInt(
        SettingKeys.AuctionCacheRefreshSeconds,
        60
    )
);

});


app.MapGet("/api/admin/settings/auction-check-interval",
(
    SettingsService settings
) =>
{

    return Results.Ok(
settings.GetInt(
    SettingKeys.AuctionCheckIntervalSeconds,
    60
)
);

});

app.MapPost("/api/admin/settings/auction-check-interval/{seconds}",
(
    int seconds,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.AuctionCheckIntervalSeconds,
        seconds
    );

    return Results.Ok(new
    {
        AuctionCheckIntervalSeconds = seconds
    });
});

app.MapPost("/api/admin/settings/auction-cache-refresh/{seconds}",
(
    int seconds,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.AuctionCacheRefreshSeconds,
        seconds
    );

    return Results.Ok(new
    {
        AuctionCacheRefreshSeconds = seconds
    });
});


app.MapGet("/api/v1/client/settings",
(
    HttpContext context,
    SettingsService settings
) =>
{
    var user = (User)context.Items["User"]!;

    return Results.Ok(new
    {
        MaxAuctionWatchesPerClient =
            settings.GetInt(
                SettingKeys.MaxAuctionWatchesPerClient,
                5
            ),

        MaxWatchedPlayers =
            settings.GetInt(
                SettingKeys.MaxWatchedPlayers,
                3
            )
    });
});


app.MapPost("/api/admin/settings/max-auction-watches/{amount}",
(
    int amount,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.MaxAuctionWatchesPerClient,
        amount
    );

    return Results.Ok();
});


app.MapGet("/api/admin/settings/max-auction-watches",
(
    SettingsService settings
) =>
{
    return Results.Ok(
        settings.GetInt(
            SettingKeys.MaxAuctionWatchesPerClient,
            10
        )
    );
});

app.MapDelete("/api/v1/auction/watch/{watchId}",
(
    HttpContext context,
    string watchId,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    if (!users.RemoveAuctionWatch(
        user.ClientId,
        watchId))
    {
        return Results.NotFound(
            "Auction watch not found."
        );
    }

    return Results.Ok();
});

app.MapGet("/api/v1/auction/watch",
(
    HttpContext context,
    UserService users
) =>
{
    var user = (User)context.Items["User"]!;

    return Results.Ok(
        users.GetAuctionWatchResponses(
            user.ClientId
        )
    );
});

app.MapPost("/api/v1/auction/watch",
(
    HttpContext context,
    UserService users,
    AuctionWatchRequest request
) =>
{
    var user = (User)context.Items["User"]!;

    var watch = new AuctionWatch
    {
        ClientId = user.ClientId,

        ItemTag = request.ItemTag
            .Trim()
            .ToUpperInvariant(),

        Tier = request.Tier,

        Stars = request.Stars,

        Recombobulated = request.Recombobulated,

        PetXp = request.PetXp,

        NotifyBelow = request.NotifyBelow
    };


    var result = users.AddAuctionWatch(watch);

    return result switch
    {
        AuctionWatchAddResult.Success =>
            Results.Ok(watch),

        AuctionWatchAddResult.Duplicate =>
            Results.BadRequest(
                "This auction watch already exists."
            ),

        AuctionWatchAddResult.LimitReached =>
            Results.BadRequest(
                "You have reached your auction watch limit."
            ),

        _ =>
            Results.BadRequest()
    };
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

/*
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
*/
/*
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
*/
/*
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
*/
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
/*
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
    "Player is already being watched."
);
        }

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});
*/
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

app.MapPost("/api/admin/users/purge-inactive/{days}",
(
    int days,
    UserService users
) =>
{
    int removed = users.PurgeInactiveUsers(days);

    return Results.Ok(new
    {
        removed
    });
});

app.MapGet("/api/v1/status",
(
    HttpContext context,
    HypixelService hypixel,
    UserService users
) =>
{

    var user = (User)context.Items["User"]!;
    users.UpdateLastSeen(user.ClientId);
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
/*

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

*/
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
app.MapGet("/api/admin/validate", () =>
{
    return Results.Ok(new
    {
        valid = true
    });
});
app.MapPost("/api/admin/settings/max-requests-per-minute/{requests}",
(
    int requests,
    SettingsService settings
) =>
{
    settings.SetInt(
        SettingKeys.MaxRequestsPerMinute,
        requests);

    return Results.Ok();
});

app.MapGet("/api/admin/settings/max-requests-per-minute",
(
    SettingsService settings
) =>
{
    return Results.Ok(
        settings.GetInt(
            SettingKeys.MaxRequestsPerMinute,
            60
        )
    );
});

app.MapDelete("/api/admin/users/{clientId}",
(
    string clientId,
    UserService users
) =>
{
    bool deleted = users.DeleteUser(clientId);

    return deleted
        ? Results.Ok()
        : Results.NotFound();
});

app.Run();
record RegisterRequest(string Username);


record AuctionWatchRequest(
    string ItemTag,
    string? Tier,
    int? Stars,
    bool? Recombobulated,
    long? PetXp,
    long NotifyBelow
);


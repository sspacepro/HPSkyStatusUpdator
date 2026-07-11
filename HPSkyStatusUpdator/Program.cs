using HPSkyStatusUpdator.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient<HypixelService>();

builder.Services.AddSingleton<HypixelService>();

builder.Services.AddHostedService<HypixelUpdater>();

builder.Services.AddSingleton<UserService>();

var app = builder.Build();


var hypixel = app.Services.GetRequiredService<HypixelService>();


app.MapGet("/api/v1/status",
(
    HttpContext context,
    HypixelService hypixel,
    UserService users,
    IConfiguration config
) =>
{
    var user = users.Authenticate(context);

    if (user == null)
        return Results.Unauthorized();

    int maxRequests =
        config.GetValue<int>("Settings:MaxRequestsPerMinute");

    if (!users.CheckRateLimit(user, maxRequests))
        return Results.StatusCode(429);

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
    RegisterRequest request
) =>
{
    try
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var user = users.Register(request.Username, ip);

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

app.Run();
record RegisterRequest(string Username);


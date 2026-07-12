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

var app = builder.Build();


var hypixel = app.Services.GetRequiredService<HypixelService>();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

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

app.Run();
record RegisterRequest(string Username);


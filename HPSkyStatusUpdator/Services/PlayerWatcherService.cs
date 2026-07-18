using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class PlayerWatcherService : BackgroundService
{
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly HypixelPlayerService _hypixelPlayers;
    private readonly NotificationService _notifications;
    public PlayerWatcherService(
        SettingsService settings,
        UserService users,
        HypixelPlayerService hypixelPlayers,
        NotificationService notifications)
    {
        _settings = settings;
        _users = users;
        _hypixelPlayers = hypixelPlayers;
        _notifications = notifications;
    }
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var players = _users.GetUniqueWatchedPlayers();

            foreach (var player in players)
            {
                try
                {
                    var status =
                        await _hypixelPlayers.GetStatusByUuid(player.Uuid);
                    var oldStatus = _users.GetPlayerStatus(player.Username);

                    if (oldStatus != null)
                    {
                        string? message = null;

                        if (oldStatus.SkyBlockOnline != status.SkyBlockOnline)
                        {
                            message = status.SkyBlockOnline
                                ? "Entered SkyBlock"
                                : "Left SkyBlock";
                        }
                        else if (
                            status.SkyBlockOnline &&
                            oldStatus.Mode != status.Mode
                        )
                        {
                            message = $"Changed to {status.DisplayMode}";
                        }

                        if (message != null)
                        {
                            foreach (var clientId in _users.GetClientsWatching(player.Uuid))
                            {
                                _notifications.Add(
                                    clientId,
                                    new Notification
                                    {
                                        ClientId = clientId,
                                        Type = "player",
                                        Title = player.Username,
                                        Message = message
                                    }
                                );
                            }
                        }
                    }
                    _users.UpdatePlayerStatus(
                        player.Username,
                        status.SkyBlockOnline,
                        status.Mode
                    );

                    Console.WriteLine(
                        $"{player.Username}: Online={status.SkyBlockOnline}, Mode={status.Mode}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error checking {player.Username}: {ex.Message}"
                    );
                }
            }

            int seconds = _settings.GetInt(
                "PlayerCheckIntervalSeconds",
                60
            );

            await Task.Delay(
                TimeSpan.FromSeconds(seconds),
                stoppingToken
            );
        }
    }
}
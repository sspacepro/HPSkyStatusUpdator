namespace HPSkyStatusUpdator.Services;

public class PlayerWatcherService : BackgroundService
{
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly HypixelPlayerService _hypixelPlayers;
    public PlayerWatcherService(
        SettingsService settings,
        UserService users,
        HypixelPlayerService hypixelPlayers)
    {
        _settings = settings;
        _users = users;
        _hypixelPlayers = hypixelPlayers;
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
                        await _hypixelPlayers.GetStatus(player);

                    _users.UpdatePlayerStatus(
                        player,
                        status.SkyBlockOnline,
                        status.Mode
                    );

                    Console.WriteLine(
                        $"{player}: Online={status.SkyBlockOnline}, Mode={status.Mode}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error checking {player}: {ex.Message}"
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
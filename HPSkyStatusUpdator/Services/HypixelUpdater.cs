using HPSkyStatusUpdator.Configuration;

namespace HPSkyStatusUpdator.Services;

public class HypixelUpdater : BackgroundService
{
    private readonly HypixelService _hypixelService;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    public HypixelUpdater(
        HypixelService hypixelService,
        SettingsService settings,
        ServiceHealthService health)
    {
        _hypixelService = hypixelService;
        _settings = settings;
        _health = health;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("HypixelUpdater");
            await _hypixelService.Update();

            int seconds = _settings.GetInt(
                SettingKeys.HypixelUpdateIntervalSeconds,
                60
            );
            await Task.Delay(
                TimeSpan.FromSeconds(seconds),
                stoppingToken
                        );
        }
    }
}
using HPSkyStatusUpdator.Configuration;

namespace HPSkyStatusUpdator.Services;

public class HypixelUpdater : BackgroundService
{
    private readonly HypixelService _hypixelService;
    private readonly SettingsService _settings;

    public HypixelUpdater(
        HypixelService hypixelService,
        SettingsService settings)
    {
        _hypixelService = hypixelService;
        _settings = settings;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
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
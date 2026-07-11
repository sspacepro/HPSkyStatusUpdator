namespace HPSkyStatusUpdator.Services;

public class HypixelUpdater : BackgroundService
{
    private readonly HypixelService _hypixelService;
    private readonly IConfiguration _config;

    public HypixelUpdater(
        HypixelService hypixelService,
        IConfiguration config)
    {
        _hypixelService = hypixelService;
        _config = config;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int minutes = _config
            .GetValue<int>("Settings:UpdateMinutes");


        while (!stoppingToken.IsCancellationRequested)
        {
            await _hypixelService.Update();


            await Task.Delay(
                TimeSpan.FromMinutes(minutes),
                stoppingToken
            );
        }
    }
}
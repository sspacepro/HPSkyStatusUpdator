using HPSkyStatusUpdator.Configuration;

namespace HPSkyStatusUpdator.Services;

public class AuctionCacheService : BackgroundService
{
    private readonly AuctionService _auctions;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    private readonly ILogger<AuctionCacheService> _logger;
    public AuctionCacheService(
        AuctionService auctions,
        SettingsService settings,
        ServiceHealthService health,
        ILogger<AuctionCacheService> logger)
    {
        _auctions = auctions;
        _settings = settings;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("AuctionCacheService");
            try
            {
                await _auctions.RefreshCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auction refresh failed");
            }

            int seconds = _settings.GetInt(
                SettingKeys.AuctionCacheRefreshSeconds,
                60
            );

            await Task.Delay(
                TimeSpan.FromSeconds(seconds),
                stoppingToken);
        }
    }
}
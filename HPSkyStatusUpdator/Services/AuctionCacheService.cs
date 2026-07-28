using HPSkyStatusUpdator.Configuration;
using System.Runtime;

namespace HPSkyStatusUpdator.Services;

public class AuctionCacheService : BackgroundService
{
    private readonly AuctionService _auctions;

    public AuctionCacheService(
        AuctionService auctions)
    {
        _auctions = auctions;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _auctions.RefreshCache();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Auction refresh failed: {ex.Message}"
                );
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

    private readonly SettingsService _settings;

    public AuctionCacheService(
        AuctionService auctions,
        SettingsService settings)
    {
        _auctions = auctions;
        _settings = settings;
    }
}
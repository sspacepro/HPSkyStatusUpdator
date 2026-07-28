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


            await Task.Delay(
                TimeSpan.FromMinutes(1),
                stoppingToken);
        }
    }
}
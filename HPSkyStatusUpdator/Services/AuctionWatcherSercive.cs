using HPSkyStatusUpdator.Models;
namespace HPSkyStatusUpdator.Services;

public class AuctionWatcherService : BackgroundService
{
    private readonly UserService _users;
    private readonly AuctionService _auctions;
    private readonly SettingsService _settings;

    public AuctionWatcherService(
        UserService users,
        AuctionService auctions,
        SettingsService settings)
    {
        _users = users;
        _auctions = auctions;
        _settings = settings;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var watches = _users.GetAuctionWatches();
            var searches = watches
                .Select(w => new AuctionSearch
                {
                    ItemTag = w.ItemTag,
                    Tier = w.Tier,
                    Stars = w.Stars,
                    Recombobulated = w.Recombobulated,
                    PetLevel = w.PetLevel
                })
                .Distinct()
                .ToList();

            foreach (var search in searches)
            {
                try
                {
                    var result =
                        await _auctions.GetLowestBin(search);

                    Console.WriteLine(
                        $"{search.ItemTag}: {(result == null ? "unavailable" : result.LowestBin.ToString("N0"))}"
                    );


                    foreach (var watch in watches)
                    {
                        if (watch.ItemTag != search.ItemTag)
                            continue;

                        if (watch.Tier != search.Tier)
                            continue;

                        if (watch.Stars != search.Stars)
                            continue;

                        if (watch.Recombobulated != search.Recombobulated)
                            continue;

                        if (watch.PetLevel != search.PetLevel)
                            continue;


                        if (result == null)
                        {
                            _users.UpdateAuctionPrice(
                                watch,
                                0,
                                false
                            );
                        }
                        else
                        {
                            _users.UpdateAuctionPrice(
                                watch,
                                result.LowestBin,
                                true
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Auction error {search.ItemTag}: {ex.Message}"
                    );
                }
            }


            int seconds = _settings.GetInt(
                "AuctionCheckIntervalSeconds",
                60
            );


            await Task.Delay(
                TimeSpan.FromSeconds(seconds),
                stoppingToken
            );
        }
    }
}
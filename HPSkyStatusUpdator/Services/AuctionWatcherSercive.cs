using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class AuctionWatcherService : BackgroundService
{
    private readonly UserService _users;
    private readonly AuctionService _auctions;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;

    public AuctionWatcherService(
        UserService users,
        AuctionService auctions,
        SettingsService settings,
        NotificationService notifications)
    {
        _users = users;
        _auctions = auctions;
        _settings = settings;
        _notifications = notifications;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var watches = _users.GetAuctionWatches();

            Console.WriteLine($"Auction watches: {watches.Count}");

            foreach (var watch in watches)
            {
                Console.WriteLine(
                    $"Watching {watch.ItemTag} Tier:{watch.Tier} Stars:{watch.Stars}"
                );
            }

            var searches = watches
                .Select(w => new AuctionSearch
                {
                    ItemTag = w.ItemTag,
                    Tier = w.Tier,
                    Stars = w.Stars,
                    Recombobulated = w.Recombobulated,
                    PetXp = w.PetXp
                })
                .Distinct()
                .ToList();


            foreach (var search in searches)
            {
                try
                {
                    // One CoflNet request per unique search
                    var result =
                        await _auctions.GetLowestBin(search);


                    Console.WriteLine(
                        $"{search.ItemTag}: {(result == null ? "unavailable" : result.LowestBin.ToString("N0"))}"
                    );


                    foreach (var watch in watches)
                    {
                        // Only update watches matching this search
                        if (watch.ItemTag != search.ItemTag)
                            continue;

                        if (watch.Tier != search.Tier)
                            continue;

                        if (watch.Stars != search.Stars)
                            continue;

                        if (watch.Recombobulated != search.Recombobulated)
                            continue;

                        if (watch.PetXp != search.PetXp)
                            continue;


                        // Item no longer available
                        if (result == null)
                        {
                            _users.UpdateAuctionPrice(
                                watch,
                                0,
                                false
                            );

                            continue;
                        }


                        // Send notification only for a new price
                        if (result.LowestBin <= watch.NotifyBelow
                            && result.LowestBin != watch.LastLowestBin)
                        {
                            _notifications.Add(
                                watch.ClientId,
                                new Models.Notification
                                {
                                    ClientId = watch.ClientId,

                                    Type = "AUCTION",

                                    Title =
                                        $"{watch.ItemTag} Found",

                                    Message =
                                        $"{watch.ItemTag} is {result.LowestBin:N0} coins."
                                }
                            );
                        }


                        // Always update the current price
                        _users.UpdateAuctionPrice(
                            watch,
                            result.LowestBin,
                            true
                        );


                        Console.WriteLine(
                            $"Updated {watch.ClientId}: {watch.ItemTag} {result.LowestBin:N0}"
                        );
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
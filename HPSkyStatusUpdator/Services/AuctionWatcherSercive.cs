using HPSkyStatusUpdator.Configuration;
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
        DateTime lastCleanup = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            var watches = _users.GetAuctionWatches();
            int cleanupMinutes = _settings.GetInt(
                SettingKeys.WatchCleanupIntervalMinutes,
                     60);

            if (DateTime.UtcNow - lastCleanup >
                TimeSpan.FromMinutes(cleanupMinutes))
            {
                _users.DeleteExpired();

                lastCleanup = DateTime.UtcNow;

                Console.WriteLine($"Deleted expired watches.");
            }

            foreach (var watch in watches)
            {
                Console.WriteLine(
                    $"Auction: {watch.ItemTag} | Tier:{watch.Tier} | Stars:{watch.Stars} | Recomb:{watch.Recombobulated} | XP:{watch.PetXp}"
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
                    var auctions = _auctions.SearchAuctions(search)
                          ?? new List<DecodedAuction>();
                    



                    var lowest = auctions
                         .Where(a =>
                            search.Tier == null ||
                             string.Equals(
                                a.Tier,
                                search.Tier,
                                StringComparison.OrdinalIgnoreCase))


                        .Where(a =>
                            search.Stars == null ||
                            (a.Stars != null && a.Stars >= search.Stars))

                        .Where(a =>
                        {
                            if (search.Recombobulated == null)
                                return true;

                            if (search.Recombobulated == false)
                                return true; // allow both

                            return a.Recombobulated == true; // only recombed
                        })

                        .Where(a =>
                            search.PetXp == null ||
                            a.PetXp == null ||
                            a.PetXp >= search.PetXp)

                        .OrderBy(a => a.Price)
                        .FirstOrDefault();


                    Console.WriteLine(
                        $"{search.ItemTag}: {(lowest == null ? "unavailable" : lowest.Price.ToString("N0"))}"
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
                        if (lowest == null)
                        {
                            _users.UpdateAuctionPrice(
                                watch,
                                0,
                                "",
                                "",
                                false
                            );

                            continue;
                        }


                        // Send notification only for a new price
                        if (lowest.Price <= watch.NotifyBelow
                        && lowest.Price != watch.LastLowestBin)
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
                                        $"{watch.ItemTag} is {lowest.Price:N0} coins."
                                }
                            );
                        }


                        // Always update the current price
                        _users.UpdateAuctionPrice(
                            watch,
                            lowest.Price,
                            lowest.DisplayItemName,
                            lowest.ItemLore,
                            true
                        );


                        Console.WriteLine(
                            $"Updated {watch.ClientId}: {watch.ItemTag} {lowest.Price:N0}"
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
                120
            );



            await Task.Delay(
                TimeSpan.FromSeconds(seconds),
                stoppingToken);
        }
    }
}
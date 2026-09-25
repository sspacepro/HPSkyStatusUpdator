using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class AuctionWatcherService : BackgroundService
{
    private readonly UserService _users;
    private readonly AuctionService _auctions;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly ServiceHealthService _health;
    private readonly ILogger<AuctionWatcherService> _logger;

    public AuctionWatcherService(
        UserService users,
        AuctionService auctions,
        SettingsService settings,
        NotificationService notifications,
        ServiceHealthService health,
        ILogger<AuctionWatcherService> logger)
    {
        _users = users;
        _auctions = auctions;
        _settings = settings;
        _notifications = notifications;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime lastCleanup = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("AuctionWatcherService");

            var watches = _users.GetAuctionWatches();

            int cleanupMinutes = _settings.GetInt(
                SettingKeys.WatchCleanupIntervalMinutes,
                60);

            if (DateTime.UtcNow - lastCleanup > TimeSpan.FromMinutes(cleanupMinutes))
            {
                _users.DeleteExpired();
                lastCleanup = DateTime.UtcNow;
                _logger.LogInformation("Deleted expired watches.");
            }

            var searches = watches
                .Select(w => new AuctionSearch
                {
                    ItemTag = w.ItemTag,
                    Tier = w.Tier,
                })
                .Distinct()
                .ToList();

            foreach (var search in searches)
            {
                try
                {
                    // The SQL query already applies the full filter set
                    // (tier/stars/recomb/pet xp), so no further in-memory
                    // filtering is needed here — just fan the one result
                    // out to every watch with matching criteria.
                    DecodedAuction? lowest = _auctions.GetCheapestMatch(search);

                    _logger.LogInformation(
                        "{ItemTag}: {Price}",
                        search.ItemTag,
                        lowest == null ? "unavailable" : lowest.Price.ToString("N0"));

                    foreach (var watch in watches)
                    {
                        if (watch.ItemTag != search.ItemTag)
                            continue;

                        if (watch.Tier != search.Tier)
                            continue;


                        if (lowest == null)
                        {
                            _users.UpdateAuctionPrice(watch, 0, "", "", false);
                            continue;
                        }

                        if (lowest.Price <= watch.NotifyBelow
                            && lowest.Price != watch.LastLowestBin)
                        {
                            _notifications.Add(
                                watch.ClientId,
                                new Models.Notification
                                {
                                    ClientId = watch.ClientId,
                                    Type = "AUCTION",
                                    Title = $"{watch.ItemTag} Found",
                                    Message = $"{watch.ItemTag} is {lowest.Price:N0} coins."
                                });
                        }

                        _users.UpdateAuctionPrice(
                            watch,
                            lowest.Price,
                            lowest.DisplayItemName,
                            lowest.ItemLore,
                            true);

                        _logger.LogInformation(
                            "Updated {ClientId}: {ItemTag} {Price}",
                            watch.ClientId,
                            watch.ItemTag,
                            lowest.Price.ToString("N0"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auction error {ItemTag}: {Message}", search.ItemTag, ex.Message);
                }
            }

            int seconds = _settings.GetInt("AuctionCheckIntervalSeconds", 120);

            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }
}

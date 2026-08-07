using HPSkyStatusUpdator.Services;

namespace HPSkyStatusUpdator.Services;

public class HealthService
{
    private readonly DatabaseService _database;
    private readonly AuctionService _auctions;
    private readonly ItemCacheService _items;
    private readonly HypixelService _hypixel;

    public HealthService(
        DatabaseService database,
        AuctionService auctions,
        ItemCacheService items,
        HypixelService hypixel)
    {
        _database = database;
        _auctions = auctions;
        _items = items;
        _hypixel = hypixel;
    }


    public object GetStatus()
    {
        bool databaseOk = false;

        try
        {
            using var connection =
                _database.GetConnection();

            connection.Open();

            databaseOk = true;
        }
        catch
        {
            databaseOk = false;
        }


        return new
        {
            Status = databaseOk
                ? "Healthy"
                : "Unhealthy",

            Database = databaseOk,

            AuctionCache = new
            {
                Loaded = _auctions
                    .GetAllAuctions()
                    .Count,

                Ready = _auctions
                    .GetAllAuctions()
                    .Count > 0
            },

            ItemCache = new
            {
                Loaded = _items
                    .GetItems()
                    .Count,

                Ready = _items
                    .GetItems()
                    .Count > 0
            },

            Hypixel = new
            {
                SkyBlockPlayers =
                    _hypixel.GetSkyblockPlayers()
            },

            Time = DateTime.UtcNow
        };
    }
}
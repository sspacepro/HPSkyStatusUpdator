using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

public class MarketDatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<MarketDatabaseService> _logger;

    public MarketDatabaseService(ILogger<MarketDatabaseService> logger)
    {
        _logger = logger;

        var dataPath =
            Environment.GetEnvironmentVariable("DATA_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "Data");

        Directory.CreateDirectory(dataPath);

        var databasePath = Path.Combine(dataPath, "market.db");

        _connectionString = $"Data Source={databasePath}";

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText =
        """
        PRAGMA journal_mode=WAL;
        PRAGMA synchronous=NORMAL;
        """;
        pragmaCommand.ExecuteNonQuery();

        CreateSchema(connection);

        _logger.LogInformation("Market database ready at {Path}", databasePath);
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Auctions
        (
            Uuid TEXT NOT NULL PRIMARY KEY,
            ItemTag TEXT NOT NULL,
            Tier TEXT,
            Price INTEGER NOT NULL,
            DisplayItemName TEXT NOT NULL DEFAULT '',
            ItemLore TEXT NOT NULL DEFAULT '',
            Attributes TEXT NOT NULL DEFAULT '{}',
            Extras TEXT NOT NULL DEFAULT '{}',
            StartTime INTEGER NOT NULL,
            EndTime INTEGER NOT NULL,
            LastSeenAt INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Auctions_ItemTag_Price
            ON Auctions(ItemTag, Price);

        -- Sale history is kept forever (per your call). Rows are small
        -- (scalars + one computed ComponentValue float instead of the
        -- full raw attribute set) specifically so that's affordable.
        CREATE TABLE IF NOT EXISTS SaleHistory
        (
            Uuid TEXT NOT NULL PRIMARY KEY,
            ItemTag TEXT NOT NULL,
            Tier TEXT,
            FinalPrice INTEGER NOT NULL,
            SoldAt INTEGER NOT NULL,
            Stars INTEGER,
            RarityUpgrades INTEGER NOT NULL DEFAULT 0,
            HotPotatoCount INTEGER,
            PetLevel INTEGER,
            ComponentValue REAL,
            Extras TEXT NOT NULL DEFAULT '{}',
            IsOutlier INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS IX_SaleHistory_ItemTag_SoldAt
            ON SaleHistory(ItemTag, SoldAt);

        CREATE TABLE IF NOT EXISTS BazaarPrices
        (
            ProductId TEXT NOT NULL PRIMARY KEY,
            BuyPrice REAL NOT NULL,
            SellPrice REAL NOT NULL,
            CapturedAt INTEGER NOT NULL
        );

        -- Not populated yet (comes with the rollup job next pass) but
        -- created now so nothing else needs another migration for it.
        CREATE TABLE IF NOT EXISTS DailyPriceAggregates
        (
            ItemTag TEXT NOT NULL,
            Date TEXT NOT NULL,
            MinPrice INTEGER,
            MedianPrice INTEGER,
            P90Price INTEGER,
            SaleCount INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (ItemTag, Date)
        );
        """;
        command.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}

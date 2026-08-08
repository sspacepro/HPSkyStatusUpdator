using HPSkyStatusUpdator.Models;
using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;
    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;

        var dataPath =
            Environment.GetEnvironmentVariable("DATA_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "Data");


        Directory.CreateDirectory(dataPath);


        var databasePath =
            Path.Combine(dataPath, "hpstatus.db");


        _connectionString =
            $"Data Source={databasePath};Foreign Keys=True";


        using var connection =
            new SqliteConnection(_connectionString);

        connection.Open();
        var pragmaCommand = connection.CreateCommand();

        pragmaCommand.CommandText =
        """
        PRAGMA journal_mode=WAL;
        PRAGMA synchronous=NORMAL;
        PRAGMA foreign_keys=ON;
        """;

        pragmaCommand.ExecuteNonQuery();
        var migrationCommand = connection.CreateCommand();

        migrationCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Migrations
        (
            Version INTEGER PRIMARY KEY
        );
        """;

        migrationCommand.ExecuteNonQuery();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Users
        (
            Username TEXT NOT NULL UNIQUE,
            ClientId TEXT NOT NULL PRIMARY KEY,
            Blocked INTEGER NOT NULL,
            LastIp TEXT NOT NULL
        );
        """;

        command.ExecuteNonQuery();
        var settingsCommand = connection.CreateCommand();

        settingsCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Settings
        (
            Key TEXT NOT NULL PRIMARY KEY,
            Value TEXT NOT NULL
        );
        """;

        settingsCommand.ExecuteNonQuery();

        var watchListCommand = connection.CreateCommand();

        watchListCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS WatchList
        (
            ClientId TEXT NOT NULL,
            Username TEXT NOT NULL,
            Uuid TEXT NOT NULL,

            PRIMARY KEY(ClientId, Uuid),

            FOREIGN KEY(ClientId)
                REFERENCES Users(ClientId)
                ON DELETE CASCADE
        );
        """;

        watchListCommand.ExecuteNonQuery();

        var playerStatusCommand = connection.CreateCommand();

        playerStatusCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS PlayerStatus
        (
            Username TEXT NOT NULL PRIMARY KEY,
            SkyBlockOnline INTEGER NOT NULL,
            Mode TEXT NOT NULL
        );
        """;

        playerStatusCommand.ExecuteNonQuery();

        var auctionWatchCommand = connection.CreateCommand();

        auctionWatchCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS AuctionWatchList
        (
            WatchId TEXT NOT NULL PRIMARY KEY,

            ClientId TEXT NOT NULL,
            ItemTag TEXT NOT NULL,

            Tier TEXT,
            Stars INTEGER,
            Recombobulated INTEGER,
            PetXp INTEGER,

            NotifyBelow INTEGER NOT NULL,
            LastLowestBin INTEGER NOT NULL DEFAULT 0,

            Available INTEGER NOT NULL DEFAULT 0,

            UNIQUE(
                ClientId,
                ItemTag,
                Tier,
                Stars,
                Recombobulated,
                PetXp
            ),

            FOREIGN KEY(ClientId)
                REFERENCES Users(ClientId)
                ON DELETE CASCADE
        );
        """;

        auctionWatchCommand.ExecuteNonQuery();



        var auctionStatusCommand = connection.CreateCommand();

        auctionStatusCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS AuctionStatus
        (
            ItemTag TEXT NOT NULL PRIMARY KEY,
            ItemName TEXT NOT NULL,
            LowestBin INTEGER NOT NULL,
            LastUpdated TEXT NOT NULL
        );
        """;

        auctionStatusCommand.ExecuteNonQuery();

        var knownItemsCommand = connection.CreateCommand();

        knownItemsCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS KnownAuctionItems
        (
            Id TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            Tier TEXT,
            CanRecombobulate INTEGER
        );
        """;

        knownItemsCommand.ExecuteNonQuery();

        

    }



    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private bool HasMigration(
    SqliteConnection connection,
    int version)
    {
        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT COUNT(*)
    FROM Migrations
    WHERE Version = $version
    """;

        command.Parameters.AddWithValue(
            "$version",
            version
        );

        return (long)command.ExecuteScalar()! > 0;
    }

    private void AddMigration(
        SqliteConnection connection,
        int version)
    {
        var command = connection.CreateCommand();

        command.CommandText =
        """
    INSERT INTO Migrations
    (
        Version
    )
    VALUES
    (
        $version
    )
    """;

        command.Parameters.AddWithValue(
            "$version",
            version
        );

        command.ExecuteNonQuery();
    }
    public void UpsertKnownAuctionItem(HypixelItem item)
    {
        using var connection = GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
    INSERT INTO KnownAuctionItems
    (
        Id,
        Name,
        Tier,
        CanRecombobulate
    )
    VALUES
    (
        $id,
        $name,
        $tier,
        $recomb
    )
    ON CONFLICT(Id)
    DO UPDATE SET
        Name = excluded.Name,
        Tier = excluded.Tier,
        CanRecombobulate = excluded.CanRecombobulate;
    """;

        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$name", item.Name);
        command.Parameters.AddWithValue("$tier", (object?)item.Tier ?? DBNull.Value);
        command.Parameters.AddWithValue("$recomb",
            item.CanRecombobulate.HasValue
                ? item.CanRecombobulate.Value ? 1 : 0
                : DBNull.Value);

        command.ExecuteNonQuery();
    }

    public List<HypixelItem> GetKnownAuctionItems()
    {
        using var connection = GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        Id,
        Name,
        Tier,
        CanRecombobulate
    FROM KnownAuctionItems
    ORDER BY Name;
    """;

        using var reader = command.ExecuteReader();

        List<HypixelItem> items = new();

        while (reader.Read())
        {
            items.Add(new HypixelItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Tier = reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2),
                CanRecombobulate = reader.IsDBNull(3)
                    ? null
                    : reader.GetInt32(3) == 1
            });
        }

        return items;
    }
}
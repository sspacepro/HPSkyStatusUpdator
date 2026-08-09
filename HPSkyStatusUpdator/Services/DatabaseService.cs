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

        var migrationTableCommand = connection.CreateCommand();
        migrationTableCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Migrations
        (
            Version INTEGER PRIMARY KEY
        );
        """;
        migrationTableCommand.ExecuteNonQuery();

        CreateSchema(connection);
        ApplyMigrations(connection);
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Users
        (
            Username TEXT NOT NULL UNIQUE,
            ClientId TEXT NOT NULL PRIMARY KEY,
            Blocked INTEGER NOT NULL,
            LastIp TEXT NOT NULL,
            LastSeen TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
        );

        CREATE TABLE IF NOT EXISTS Settings
        (
            Key TEXT NOT NULL PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS WatchList
        (
            ClientId TEXT NOT NULL,
            Username TEXT NOT NULL,
            Uuid TEXT NOT NULL,
            ExpiresAt TEXT,

            PRIMARY KEY(ClientId, Uuid),

            FOREIGN KEY(ClientId)
                REFERENCES Users(ClientId)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS PlayerStatus
        (
            Username TEXT NOT NULL PRIMARY KEY,
            SkyBlockOnline INTEGER NOT NULL,
            Mode TEXT NOT NULL
        );

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
            LastDisplayItemName TEXT NOT NULL DEFAULT '',
            LastItemLore TEXT NOT NULL DEFAULT '',
            Available INTEGER NOT NULL DEFAULT 0,
            ExpiresAt TEXT,

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

        CREATE TABLE IF NOT EXISTS AuctionStatus
        (
            ItemTag TEXT NOT NULL PRIMARY KEY,
            ItemName TEXT NOT NULL,
            LowestBin INTEGER NOT NULL,
            LastUpdated TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS KnownAuctionItems
        (
            Id TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            Tier TEXT,
            CanRecombobulate INTEGER
        );
        """;
        command.ExecuteNonQuery();
    }

    private void ApplyMigrations(SqliteConnection connection)
    {
        if (HasMigration(connection, 1))
            return;

        EnsureColumn(
            connection,
            "Users",
            "LastSeen",
            "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");

        EnsureColumn(
            connection,
            "WatchList",
            "ExpiresAt",
            "TEXT");

        EnsureColumn(
            connection,
            "AuctionWatchList",
            "LastDisplayItemName",
            "TEXT NOT NULL DEFAULT ''");

        EnsureColumn(
            connection,
            "AuctionWatchList",
            "LastItemLore",
            "TEXT NOT NULL DEFAULT ''");

        EnsureColumn(
            connection,
            "AuctionWatchList",
            "ExpiresAt",
            "TEXT");

        // Existing watches created before expiration existed should still expire.
        var backfill = connection.CreateCommand();
        backfill.CommandText =
        """
        UPDATE AuctionWatchList
        SET ExpiresAt = $expiresAt
        WHERE ExpiresAt IS NULL;
        """;
        backfill.Parameters.AddWithValue(
            "$expiresAt",
            DateTime.UtcNow.AddDays(30));
        backfill.ExecuteNonQuery();

        AddMigration(connection, 1);
        _logger.LogInformation("Applied database migration version 1.");
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        string table,
        string column,
        string columnDefinition)
    {
        if (ColumnExists(connection, table, column))
            return;

        var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition};";
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        string table,
        string column)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(
                reader.GetString(1),
                column,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static bool HasMigration(
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
        command.Parameters.AddWithValue("$version", version);
        return (long)command.ExecuteScalar()! > 0;
    }

    private static void AddMigration(
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
        command.Parameters.AddWithValue("$version", version);
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

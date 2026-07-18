using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

public class DatabaseService
{
    private readonly string _connectionString =
        "Data Source=Data/hpstatus.db";

    public DatabaseService()
    {
        Directory.CreateDirectory("Data");

        using var connection =
            new SqliteConnection(_connectionString);

        connection.Open();
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
            ClientId TEXT NOT NULL,
            ItemTag TEXT NOT NULL,

            Tier TEXT,
            Stars INTEGER,
            Recombobulated INTEGER,
            PetLevel INTEGER,

            NotifyBelow INTEGER NOT NULL,
            LastLowestBin INTEGER NOT NULL DEFAULT 0,

            PRIMARY KEY(ClientId, ItemTag),

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



        if (!HasMigration(connection, 1))
        {
            Console.WriteLine("Applying migration 1...");


            AddMigration(connection, 1);
        }

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
}
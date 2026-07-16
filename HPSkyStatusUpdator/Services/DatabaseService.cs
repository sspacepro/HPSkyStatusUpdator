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

            PRIMARY KEY(ClientId, Username),

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
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
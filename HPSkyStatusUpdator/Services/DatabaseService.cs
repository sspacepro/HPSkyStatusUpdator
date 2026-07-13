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
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
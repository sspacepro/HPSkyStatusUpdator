using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

public class SettingsService
{
    private readonly DatabaseService _database;

    public SettingsService(DatabaseService database)
    {
        _database = database;
    }

    public string? Get(string key)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Value
        FROM Settings
        WHERE Key = $key
        """;

        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() as string;
    }
    public string? GetString(string key)
    {
        return Get(key);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        string? value = Get(key);

        return int.TryParse(value, out int result)
            ? result
            : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        string? value = Get(key);

        return bool.TryParse(value, out bool result)
            ? result
            : defaultValue;
    }

    public void SetString(string key, string value)
    {
        Set(key, value);
    }

    public void SetInt(string key, int value)
    {
        Set(key, value.ToString());
    }

    public void SetBool(string key, bool value)
    {
        Set(key, value.ToString());
    }
    public void Set(string key, string value)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO Settings(Key, Value)
        VALUES($key, $value)
        ON CONFLICT(Key)
        DO UPDATE SET Value = excluded.Value;
        """;

        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        command.ExecuteNonQuery();
    }
}
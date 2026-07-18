using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class UserService
{
    public User? Authenticate(HttpContext context)
    {
        string? clientId =
            context.Request.Headers["Client-ID"]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        User? user = GetByClientId(clientId);

        if (user == null)
            return null;

        if (user.Blocked)
            return null;
        return user;
    }
    private User? GetByClientId(string clientId)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT Username,
           ClientId,
           Blocked,
           LastIp
    FROM Users
    WHERE ClientId = $clientId
    """;

        command.Parameters.AddWithValue("$clientId", clientId);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new User
        {
            Username = reader.GetString(0),
            ClientId = reader.GetString(1),
            Blocked = reader.GetInt32(2) == 1,
            LastIp = reader.GetString(3)
        };
    }
    private readonly DatabaseService _database;

    public List<string> GetWatchedPlayers()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Username
        FROM WatchList
        """;

        using var reader = command.ExecuteReader();

        List<string> players = new();

        while (reader.Read())
        {
            players.Add(reader.GetString(0));
        }

        return players;
    }

    public void UpdatePlayerStatus(
    string username,
    bool skyBlockOnline,
    string mode)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO PlayerStatus
        (
            Username,
            SkyBlockOnline,
            Mode
        )
        VALUES
        (
            $username,
            $online,
            $mode
        )
        ON CONFLICT(Username)
        DO UPDATE SET
            SkyBlockOnline = $online,
            Mode = $mode;
        """;

        command.Parameters.AddWithValue(
            "$username",
            username
        );

        command.Parameters.AddWithValue(
            "$online",
            skyBlockOnline ? 1 : 0
        );

        command.Parameters.AddWithValue(
            "$mode",
            mode
        );

        command.ExecuteNonQuery();
    }

    public object GetPlayerStatuses(string clientId)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT 
            WatchList.Username,
            PlayerStatus.SkyBlockOnline,
            PlayerStatus.Mode
        FROM WatchList
        LEFT JOIN PlayerStatus
            ON WatchList.Username = PlayerStatus.Username
        WHERE WatchList.ClientId = $clientId
        """;

        command.Parameters.AddWithValue(
            "$clientId",
            clientId
        );

        using var reader = command.ExecuteReader();

        List<object> results = new();

        while (reader.Read())
        {
            results.Add(new
            {
                Username = reader.GetString(0),
                SkyBlockOnline = !reader.IsDBNull(1) &&
                    reader.GetInt32(1) == 1,
                Mode = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }

        return results;
    }
    public UserService(DatabaseService database)
    {
        _database = database;
    }

    public List<User> GetAllUsers()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT Username,
           ClientId,
           Blocked,
           LastIp
    FROM Users
    ORDER BY Username
    """;

        using var reader = command.ExecuteReader();

        List<User> users = new();

        while (reader.Read())
        {
            users.Add(new User
            {
                Username = reader.GetString(0),
                ClientId = reader.GetString(1),
                Blocked = reader.GetInt32(2) == 1,
                LastIp = reader.GetString(3)
            });
        }

        return users;
    }
    public int GetUserCount()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT COUNT(*)
    FROM Users
    """;

        return Convert.ToInt32(command.ExecuteScalar());
    }
    public async Task<bool> AddWatchPlayer(
        string clientId,
        string username,
        HypixelPlayerService hypixelPlayers)
    {
        username = username.Trim().ToLowerInvariant();
        string? uuid = await hypixelPlayers.GetUuid(username);

        if (uuid == null)
            throw new Exception("Player not found.");
        using var connection = _database.GetConnection();

        connection.Open();

        var countCommand = connection.CreateCommand();

        countCommand.CommandText =
        """
    SELECT COUNT(*)
    FROM WatchList
    WHERE ClientId = $clientId
    """;

        countCommand.Parameters.AddWithValue("$clientId", clientId);

        long count = (long)countCommand.ExecuteScalar()!;

        if (count >= 3)
            return false;

        var insertCommand = connection.CreateCommand();

        insertCommand.CommandText =
        """
        INSERT OR IGNORE INTO WatchList
        (
            ClientId,
            Username,
            Uuid
        )
        VALUES
        (
            $clientId,
            $username,
            $uuid
        )
        """;
        insertCommand.Parameters.AddWithValue("$clientId", clientId);
        insertCommand.Parameters.AddWithValue("$username", username);
        insertCommand.Parameters.AddWithValue("$uuid", uuid);


        insertCommand.ExecuteNonQuery();

        return true;
    }
    public bool SetBlocked(string username, bool blocked)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    UPDATE Users
    SET Blocked = $blocked
    WHERE LOWER(Username) = LOWER($username)
    """;

        command.Parameters.AddWithValue("$blocked", blocked ? 1 : 0);
        command.Parameters.AddWithValue("$username", username);

        return command.ExecuteNonQuery() > 0;
    }

    public List<string> GetUniqueWatchedPlayers()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT DISTINCT LOWER(Username)
    FROM WatchList
    """;

        using var reader = command.ExecuteReader();

        List<string> players = new();

        while (reader.Read())
        {
            players.Add(reader.GetString(0));
        }

        return players;
    }
    public User Register(string username, string ip)
    {
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(username))
            throw new Exception("Username cannot be empty.");

        using var connection = _database.GetConnection();

        connection.Open();

        // Check if username already exists
        var checkCommand = connection.CreateCommand();

        checkCommand.CommandText =
        """
    SELECT COUNT(*)
    FROM Users
    WHERE LOWER(Username) = LOWER($username)
    """;

        checkCommand.Parameters.AddWithValue("$username", username);

        long count = (long)checkCommand.ExecuteScalar()!;

        if (count > 0)
            throw new Exception("Username already exists.");

        User user = new()
        {
            Username = username,
            ClientId = Guid.NewGuid().ToString(),
            Blocked = false,
            LastIp = ip
        };

        var insertCommand = connection.CreateCommand();

        insertCommand.CommandText =
        """
    INSERT INTO Users
    (
        Username,
        ClientId,
        Blocked,
        LastIp
    )
    VALUES
    (
        $username,
        $clientId,
        $blocked,
        $lastIp
    )
    """;

        insertCommand.Parameters.AddWithValue("$username", user.Username);
        insertCommand.Parameters.AddWithValue("$clientId", user.ClientId);
        insertCommand.Parameters.AddWithValue("$blocked", user.Blocked ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$lastIp", user.LastIp);

        insertCommand.ExecuteNonQuery();

        return user;
    }
}
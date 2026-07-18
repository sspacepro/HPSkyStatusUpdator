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
            throw new Exception("Player does not exist.");

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

        int rows = insertCommand.ExecuteNonQuery();

        return rows > 0;
    }

    public List<string> GetClientsWatching(string uuid)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT ClientId
        FROM WatchList
        WHERE Uuid = $uuid
        """;

        command.Parameters.AddWithValue(
            "$uuid",
            uuid
        );

        using var reader = command.ExecuteReader();

        List<string> clients = new();

        while (reader.Read())
        {
            clients.Add(reader.GetString(0));
        }

        return clients;
    }
    public PlayerStatus? GetPlayerStatus(string username)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Username, SkyBlockOnline, Mode
        FROM PlayerStatus
        WHERE Username = $username
        """;

        command.Parameters.AddWithValue(
            "$username",
            username
        );

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new PlayerStatus
        {
            Username = reader.GetString(0),
            SkyBlockOnline = reader.GetBoolean(1),
            Mode = reader.GetString(2)
        };
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

    public bool AddAuctionWatch(AuctionWatch watch)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    INSERT OR IGNORE INTO AuctionWatchList
    (
        ClientId,
        ItemTag,
        Tier,
        Stars,
        Recombobulated,
        PetLevel,
        NotifyBelow,
        LastLowestBin
    )
    VALUES
    (
        $clientId,
        $itemTag,
        $tier,
        $stars,
        $recomb,
        $petLevel,
        $notifyBelow,
        0
    );
    """;

        command.Parameters.AddWithValue(
            "$clientId",
            watch.ClientId
        );

        command.Parameters.AddWithValue(
            "$itemTag",
            watch.ItemTag
        );

        command.Parameters.AddWithValue(
            "$tier",
            (object?)watch.Tier ?? DBNull.Value
        );

        command.Parameters.AddWithValue(
            "$stars",
            (object?)watch.Stars ?? DBNull.Value
        );

        command.Parameters.AddWithValue(
            "$recomb",
            watch.Recombobulated.HasValue
                ? watch.Recombobulated.Value ? 1 : 0
                : DBNull.Value
        );

        command.Parameters.AddWithValue(
            "$petLevel",
            (object?)watch.PetLevel ?? DBNull.Value
        );

        command.Parameters.AddWithValue(
            "$notifyBelow",
            watch.NotifyBelow
        );

        return command.ExecuteNonQuery() > 0;
    }

    public List<AuctionWatch> GetAuctionWatches()
    {
        var watches = new List<AuctionWatch>();

        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        ClientId,
        ItemTag,
        Tier,
        Stars,
        Recombobulated,
        PetLevel,
        NotifyBelow,
        LastLowestBin
    FROM AuctionWatchList;
    """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            watches.Add(new AuctionWatch
            {
                ClientId = reader.GetString(0),
                ItemTag = reader.GetString(1),

                Tier = reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2),

                Stars = reader.IsDBNull(3)
                    ? null
                    : reader.GetInt32(3),

                Recombobulated = reader.IsDBNull(4)
                    ? null
                    : reader.GetInt32(4) == 1,

                PetLevel = reader.IsDBNull(5)
                    ? null
                    : reader.GetInt32(5),

                NotifyBelow = reader.GetInt64(6),
                LastLowestBin = reader.GetInt64(7)
            });
        }

        return watches;
    }

    public void UpdateAuctionPrice(
    string clientId,
    string itemTag,
    long price)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    UPDATE AuctionWatchList
    SET LastLowestBin = $price
    WHERE ClientId = $clientId
    AND ItemTag = $itemTag;
    """;

        command.Parameters.AddWithValue(
            "$price",
            price
        );

        command.Parameters.AddWithValue(
            "$clientId",
            clientId
        );

        command.Parameters.AddWithValue(
            "$itemTag",
            itemTag
        );

        command.ExecuteNonQuery();
    }
    public bool RemoveWatchPlayer(
    string clientId,
    string username)
    {
        username = username.Trim().ToLowerInvariant();

        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    DELETE FROM WatchList
    WHERE ClientId = $clientId
    AND Username = $username
    """;

        command.Parameters.AddWithValue(
            "$clientId",
            clientId
        );

        command.Parameters.AddWithValue(
            "$username",
            username
        );

        int rows = command.ExecuteNonQuery();

        return rows > 0;
    }
    public List<object> GetWatchList(string clientId)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Username, Uuid
        FROM WatchList
        WHERE ClientId = $clientId
        """;

        command.Parameters.AddWithValue(
            "$clientId",
            clientId
        );

        using var reader = command.ExecuteReader();

        var players = new List<object>();

        while (reader.Read())
        {
            players.Add(new
            {
                Username = reader.GetString(0),
                Uuid = reader.GetString(1)
            });
        }

        return players;
    }
    public List<WatchedPlayer> GetUniqueWatchedPlayers()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT DISTINCT
        Username,
        Uuid
    FROM WatchList
    """;

        using var reader = command.ExecuteReader();

        List<WatchedPlayer> players = new();

        while (reader.Read())
        {
            players.Add(new WatchedPlayer
            {
                Username = reader.GetString(0),
                Uuid = reader.GetString(1)
            });
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
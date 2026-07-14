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

    public UserService(DatabaseService database)
    {
        _database = database;
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
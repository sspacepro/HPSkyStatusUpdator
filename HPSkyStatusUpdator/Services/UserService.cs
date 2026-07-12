using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class UserService
{
    public User? Authenticate(HttpContext context)
    {
        string? clientId = context.Request.Headers["Client-ID"]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        User? user = _users.FirstOrDefault(x =>
            x.ClientId == clientId);

        if (user == null)
            return null;

        if (user.Blocked)
            return null;

        user.LastIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
       

        return user;
    }
    private User? GetByClientId(string clientId)
    {
        return _users.FirstOrDefault(
            x => x.ClientId == clientId
        );
    }
    private readonly string _filePath = "Data/users.json";

    private List<User> _users = new();


    public UserService()
    {
        Load();
    }


    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            Save();
            return;
        }

        string json = File.ReadAllText(_filePath);

        _users = JsonSerializer.Deserialize<List<User>>(json)
                 ?? new List<User>();
    }


    private void Save()
    {
        string json = JsonSerializer.Serialize(
            _users,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        Directory.CreateDirectory("Data");

        File.WriteAllText(_filePath, json);
    }


    public User Register(string username, string ip)
    {
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(username))
            throw new Exception("Username cannot be empty.");

        if (_users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Username already exists.");

        User user = new()
        {
            Username = username,
            ClientId = Guid.NewGuid().ToString(),
            LastIp = ip
        };

        _users.Add(user);

        Save();

        return user;
    }
}
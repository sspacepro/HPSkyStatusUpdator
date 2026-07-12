namespace HPSkyStatusUpdator.Models;

public class User
{
    public string Username { get; set; } = "";
    public string ClientId { get; set; } = "";
    public bool Blocked { get; set; } = false;
    public string LastIp { get; set; } = "";
}
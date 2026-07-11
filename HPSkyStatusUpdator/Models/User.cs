namespace HPSkyStatusUpdator.Models;

public class User
{
    public string Username { get; set; } = "";
    public string ClientId { get; set; } = "";
    public bool Blocked { get; set; } = false;
    public string LastIp { get; set; } = "";
    public DateTime WindowStart { get; set; } = DateTime.UtcNow;

    public int RequestsThisWindow { get; set; } = 0;

    public DateTime LastRequest { get; set; } = DateTime.UtcNow;
}
namespace HPSkyStatusUpdator.Models;

public class AdminNotificationRequest
{
    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    // Null or empty = every account
    public List<string>? ClientIds { get; set; }
}
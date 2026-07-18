namespace HPSkyStatusUpdator.Models;

public class Notification
{
    public string ClientId { get; set; } = "";

    public string Type { get; set; } = "";

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime Created { get; set; } = DateTime.UtcNow;
}
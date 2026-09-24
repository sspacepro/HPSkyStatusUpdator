namespace HPSkyStatusUpdator.Models;

public class BlacklistedItem
{
    public string ItemTag { get; set; } = "";

    public string? Reason { get; set; }

    public DateTime AddedAt { get; set; }
}

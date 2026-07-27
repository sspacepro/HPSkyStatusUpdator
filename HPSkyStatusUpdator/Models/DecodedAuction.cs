namespace HPSkyStatusUpdator.Models;

public class DecodedAuction
{
    public string Uuid { get; set; } = "";

    public string ItemId { get; set; } = "";

    public string ItemName { get; set; } = "";

    public string Tier { get; set; } = "";

    public long Price { get; set; }
}
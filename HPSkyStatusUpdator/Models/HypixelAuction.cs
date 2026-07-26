namespace HPSkyStatusUpdator.Models;

public class HypixelAuction
{
    public string Uuid { get; set; } = "";

    public string ItemName { get; set; } = "";

    public string ItemLore { get; set; } = "";

    public string Tier { get; set; } = "";

    public long StartingBid { get; set; }

    public bool Bin { get; set; }
}
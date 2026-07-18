namespace HPSkyStatusUpdator.Models;

public class AuctionResult
{
    public string ItemTag { get; set; } = "";

    public string ItemName { get; set; } = "";

    public long LowestBin { get; set; }

    public string Tier { get; set; } = "";

    public int Stars { get; set; }

    public bool Recombobulated { get; set; }

    public int? PetLevel { get; set; }
}
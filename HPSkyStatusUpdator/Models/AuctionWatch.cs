namespace HPSkyStatusUpdator.Models;

public class AuctionWatch
{
    public string ClientId { get; set; } = "";

    public string ItemTag { get; set; } = "";

    public string? Tier { get; set; }

    public int? Stars { get; set; }

    public bool? Recombobulated { get; set; }

    public int? PetLevel { get; set; }

    public long NotifyBelow { get; set; }

    public long LastLowestBin { get; set; }
}

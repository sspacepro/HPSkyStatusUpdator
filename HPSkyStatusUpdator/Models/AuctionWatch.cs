namespace HPSkyStatusUpdator.Models;

public class AuctionWatch
{
    public string ClientId { get; set; } = "";

    public string ItemTag { get; set; } = "";

    public string? Tier { get; set; }

    public int? Stars { get; set; }

    public bool? Recombobulated { get; set; }

    public long? PetXp { get; set; }

    public string DisplayItemName { get; set; } = "";

    public string ItemLore { get; set; } = "";

    public long NotifyBelow { get; set; }

    public long LastLowestBin { get; set; }

    public bool Available { get; set; }
    public DateTime ExpiresAt { get; set; }

    public string WatchId { get; set; } = Guid.NewGuid().ToString();
}

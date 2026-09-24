namespace HPSkyStatusUpdator.Models;

public class AuctionSearch
{
    public string ItemTag { get; set; } = "";

    public string? Tier { get; set; }


    public override bool Equals(object? obj)
    {
        if (obj is not AuctionSearch other)
            return false;

        return ItemTag == other.ItemTag
            && Tier == other.Tier;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            ItemTag,
            Tier
        );
    }
}
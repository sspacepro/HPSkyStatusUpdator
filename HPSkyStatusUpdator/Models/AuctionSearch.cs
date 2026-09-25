namespace HPSkyStatusUpdator.Models;

public class AuctionSearch
{
    public string ItemTag { get; set; } = "";

    public string? Tier { get; set; }

    public int? Stars { get; set; }

    public bool? Recombobulated { get; set; }

    public long? PetXp { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not AuctionSearch other)
            return false;

        return ItemTag == other.ItemTag
            && Tier == other.Tier
            && Stars == other.Stars
            && Recombobulated == other.Recombobulated
            && PetXp == other.PetXp;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            ItemTag,
            Tier,
            Stars,
            Recombobulated,
            PetXp
        );
    }
}
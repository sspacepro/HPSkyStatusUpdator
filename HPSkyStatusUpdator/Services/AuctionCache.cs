using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class AuctionCache
{
    public List<DecodedAuction> Auctions { get; set; } = new();

    public DateTime LastUpdated { get; set; }
}
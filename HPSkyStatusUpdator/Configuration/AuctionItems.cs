using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Configuration;

public static class AuctionItems
{
    public static List<HypixelItem> CustomItems =
    [
        new HypixelItem
        {
            Id = "ENDERMAN_PET",
            Name = "Enderman Pet",
            CanRecombobulate = false,
            Custom = true
        },

        new HypixelItem
        {
            Id = "ENDER_DRAGON_PET",
            Name = "Ender Dragon Pet",
            CanRecombobulate = false,
            Custom = true
        }
    ];
}
namespace HPSkyStatusUpdator.Models;

public class DecodedAuction
{
    public string Uuid { get; set; } = "";

    public string ItemId { get; set; } = "";

    public string ItemName { get; set; } = "";

    public string Tier { get; set; } = "";

    public long Price { get; set; }

    public long Start { get; set; }

    public long End { get; set; }

    public string DisplayItemName { get; set; } = "";

    public string ItemLore { get; set; } = "";

    public ItemAttributes Attributes { get; set; } = new();

    public ItemExtras Extras { get; set; } = new();
}

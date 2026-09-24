namespace HPSkyStatusUpdator.Models;

public class DecodedItem
{
    public string ItemId { get; set; } = "";

    public ItemAttributes Attributes { get; set; } = new();
    public ItemExtras Extras { get; set; } = new();
}

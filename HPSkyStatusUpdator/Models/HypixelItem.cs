namespace HPSkyStatusUpdator.Models;

public class HypixelItem
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Tier { get; set; } = "";

    public bool? CanRecombobulate { get; set; }

    public bool Custom { get; set; }
}
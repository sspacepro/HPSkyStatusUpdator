namespace HPSkyStatusUpdator.Models;

public class HypixelStatus
{
    public bool Online { get; set; }

    public string GameType { get; set; } = "";

    public bool SkyBlockOnline =>
        Online && GameType == "SKYBLOCK";

    public string Mode { get; set; } = "";

    private static readonly Dictionary<string, string> ModeNames = new()
    {
        ["hub"] = "Hub",
        ["dynamic"] = "Private Island",
        ["dungeon"] = "Dungeon Hub",
        ["crystal_hollows"] = "Crystal Hollows",
        ["mining_3"] = "Crystal Hollows",
        ["mining_1"] = "Gold Mine",
        ["mining_2"] = "Deep Caverns",
        ["combat_1"] = "Spider's Den",
        ["combat_2"] = "The End",
        ["combat_3"] = "Crimson Isle",
        ["farming_1"] = "The Barn",
        ["farming_2"] = "Mushroom Desert",
        ["foraging_1"] = "The Park",
        ["foraging_2"] = "Galatea",
        ["rift"] = "The Rift",
        ["kuudra"] = "Kuudra",
    };

    public string DisplayMode
    {
        get
        {
            return ModeNames.TryGetValue(Mode, out var name)
                ? name
                : Mode;
        }
    }
}
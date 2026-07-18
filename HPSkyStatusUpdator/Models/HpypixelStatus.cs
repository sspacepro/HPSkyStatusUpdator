namespace HPSkyStatusUpdator.Models;

public class HypixelStatus
{
    public bool Online { get; set; }

    public bool SkyBlockOnline =>
        Online && GameType == "SKYBLOCK";

    public string GameType { get; set; } = "";

    public string Mode { get; set; } = "";

    public string DisplayMode
    {
        get
        {
            return Mode switch
            {
                "hub" => "Hub",
                "dynamic" => "Private Island",
                "dungeon" => "Dungeon Hub",
                "combat_1" => "Combat",
                _ => Mode
            };
        }
    }
}

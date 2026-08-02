/*

using System.Text.Json.Serialization;

namespace HPSkyStatusUpdator.Models;

public class PlayerStatus
{
    public string Username { get; set; } = "";
    public bool SkyBlockOnline { get; set; }

    [JsonIgnore]
    public string Mode { get; set; } = "";

    public string DisplayMode =>
        Mode switch
        {
            "hub" => "Hub",
            "dynamic" => "Private Island",
            "dungeon" => "Dungeon",
            "crystal_hollows" => "Crystal Hollows",
            "mining_3" => "Crystal Hollows",
            "mining_1" => "Gold Mine",
            "mining_2" => "Deep Caverns",
            "combat_1" => "Spider's Den",
            "combat_2" => "The End",
            "combat_3" => "Crimson Isle",
            "farming_1" => "The Barn",
            "farming_2" => "Mushroom Desert",
            "foraging_1" => "The Park",
            "foraging_2" => "Galatea",
            "rift" => "The Rift",
            "kuudra" => "Kuudra",
            _ => Mode
        };
}

*/
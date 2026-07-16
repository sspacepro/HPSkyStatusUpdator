namespace HPSkyStatusUpdator.Models;

public class HypixelStatus
{
    public bool Online { get; set; }

    public bool SkyBlockOnline =>
        Online && GameType == "SKYBLOCK";

    public string GameType { get; set; } = "";

    public string Mode { get; set; } = "";
}
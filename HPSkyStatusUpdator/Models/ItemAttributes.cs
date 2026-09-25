namespace HPSkyStatusUpdator.Models;

public class ItemAttributes
{
    public int? Stars { get; set; }              // ExtraAttributes.upgrade_level, 0-10 (base + master combined)

    public bool Recombobulated { get; set; }      // derived: RarityUpgrades > 0
    public int RarityUpgrades { get; set; }        // raw ExtraAttributes.rarity_upgrades

    public int? HotPotatoCount { get; set; }        // ExtraAttributes.hot_potato_count (hot potato + fuming potato combined)

    public Dictionary<string, int> Enchantments { get; set; } = new();

    // Raw pass-through of the "gems" compound, e.g. "JADE_0" -> "PERFECT".
    // Category slots (COMBAT_0 etc.) are stored as-is too, just not yet
    // interpreted into a specific gem type — see ComponentValueCalculator.
    public Dictionary<string, string> Gems { get; set; } = new();

    // Pet-specific fields. Null for non-pet items.
    public string? PetType { get; set; }
    public string? PetRarity { get; set; }
    public double? PetExp { get; set; }
    public int? PetLevel { get; set; }
}

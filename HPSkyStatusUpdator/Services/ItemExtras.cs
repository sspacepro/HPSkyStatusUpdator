namespace HPSkyStatusUpdator.Models;

// Catch-all for item-specific data that doesn't deserve its own column.
// Empty for the vast majority of items (cheap to store), populated for
// the handful of items that have something special going on.
public class ItemExtras
{
    public List<string> Scrolls { get; set; } = new(); // ExtraAttributes.ability_scroll

    public Dictionary<string, string> Other { get; set; } = new();
}

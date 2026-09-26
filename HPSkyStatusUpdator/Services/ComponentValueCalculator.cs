using HPSkyStatusUpdator.Models;
using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

// Estimates the coin value of the upgrades applied to an item, using
// current bazaar prices (and, for master stars, our own recent AH sale
// data). This is an approximation for prediction signal, not an exact
// cost breakdown — missing prices are skipped rather than blocking the
// calculation.
public class ComponentValueCalculator
{
    private readonly BazaarPriceService _bazaar;
    private readonly MarketDatabaseService _market;
    private readonly ILogger<ComponentValueCalculator> _logger;

    private const string RecombobulatorProduct = "RECOMBOBULATOR_3000";
    private const string HotPotatoBookProduct = "HOT_POTATO_BOOK";
    private const string FumingPotatoBookProduct = "FUMING_POTATO_BOOK";
    private const string EssenceWitherProduct = "ESSENCE_WITHER";

    private static readonly string[] NamedGemTypes =
    {
        "RUBY", "AMETHYST", "JADE", "SAPPHIRE", "AMBER", "TOPAZ",
        "JASPER", "OPAL", "ONYX", "AQUAMARINE", "CITRINE", "PERIDOT"
    };

    // Logs the "no star cost table entry" warning once per item id
    // rather than once per sale, so a common untracked item doesn't
    // spam the log.
    private readonly HashSet<string> _warnedMissingStarCost = new();

    public ComponentValueCalculator(
        BazaarPriceService bazaar,
        MarketDatabaseService market,
        ILogger<ComponentValueCalculator> logger)
    {
        _bazaar = bazaar;
        _market = market;
        _logger = logger;
    }

    public double? Calculate(string itemTag, ItemAttributes attributes)
    {
        double total = 0;
        bool foundAny = false;

        if (attributes.Recombobulated)
        {
            var price = _bazaar.GetBuyPrice(RecombobulatorProduct);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
            }
        }

        if (attributes.HotPotatoCount is > 0)
        {
            int hotPotatoBooks = Math.Min(attributes.HotPotatoCount.Value, 10);
            int fumingPotatoBooks = Math.Max(attributes.HotPotatoCount.Value - 10, 0);

            var hotPrice = _bazaar.GetBuyPrice(HotPotatoBookProduct);
            if (hotPrice.HasValue)
            {
                total += hotPotatoBooks * hotPrice.Value;
                foundAny = true;
            }

            if (fumingPotatoBooks > 0)
            {
                var fumingPrice = _bazaar.GetBuyPrice(FumingPotatoBookProduct);
                if (fumingPrice.HasValue)
                {
                    total += fumingPotatoBooks * fumingPrice.Value;
                    foundAny = true;
                }
            }
        }

        if (attributes.Stars is > 0)
        {
            var starCost = CalculateStarCost(itemTag, attributes.Stars.Value);

            if (starCost.HasValue)
            {
                total += starCost.Value;
                foundAny = true;
            }
        }

        foreach (var (slot, quality) in attributes.Gems)
        {
            string? productId = MapGemToProduct(slot, quality);

            if (productId == null)
                continue; // category slot — see MapGemToProduct

            var price = _bazaar.GetBuyPrice(productId);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
            }
        }

        foreach (var (enchant, level) in attributes.Enchantments)
        {
            string productId = $"ENCHANTMENT_{enchant.ToUpperInvariant()}_{level}";

            var price = _bazaar.GetBuyPrice(productId);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
            }
            // No price found (not bazaar-tradeable at this level) —
            // skipped silently, as agreed.
        }

        return foundAny ? total : null;
    }

    // Stars 1-5: per-item essence amount x current ESSENCE_WITHER price.
    // Stars 6-10 ("master stars"): each one is a specific dungeon-drop
    // item that only trades on the AH — priced from our own recent
    // SaleHistory instead of the bazaar.
    private double? CalculateStarCost(string itemTag, int stars)
    {
        double total = 0;
        bool foundAny = false;

        int essenceStars = Math.Min(stars, 5);

        if (essenceStars > 0)
        {
            if (StarUpgradeCosts.EssenceCostsByItem.TryGetValue(
                itemTag.ToUpperInvariant(),
                out int[]? costs))
            {
                var essencePrice = _bazaar.GetBuyPrice(EssenceWitherProduct);

                if (essencePrice.HasValue)
                {
                    int essenceUnits = 0;

                    for (int i = 0; i < essenceStars; i++)
                        essenceUnits += costs[i];

                    total += essenceUnits * essencePrice.Value;
                    foundAny = true;
                }
            }
            else if (_warnedMissingStarCost.Add(itemTag))
            {
                _logger.LogWarning(
                    "No star essence cost entry for {ItemTag} — star value will be undercounted for this item.",
                    itemTag);
            }
        }

        int masterStars = Math.Max(stars - 5, 0);

        for (int i = 0; i < masterStars && i < StarUpgradeCosts.MasterStarTags.Length; i++)
        {
            var price = GetRecentAhMedian(StarUpgradeCosts.MasterStarTags[i]);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
            }
        }

        return foundAny ? total : null;
    }

    // Median of the last 10 non-outlier sales of a given item tag from
    // our own SaleHistory. Used for master star items, which are AH-only
    // and so have no bazaar price to fall back on.
    private double? GetRecentAhMedian(string itemTag)
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT FinalPrice
        FROM SaleHistory
        WHERE ItemTag = $itemTag AND IsOutlier = 0
        ORDER BY SoldAt DESC
        LIMIT 10;
        """;
        command.Parameters.AddWithValue("$itemTag", itemTag);

        var prices = new List<long>();

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                prices.Add(reader.GetInt64(0));
        }

        if (prices.Count == 0)
            return null;

        prices.Sort();

        int mid = prices.Count / 2;

        return prices.Count % 2 == 0
            ? (prices[mid - 1] + prices[mid]) / 2.0
            : prices[mid];
    }

    // Named slots ("JADE_0") tell us the gem type from the key itself.
    // Category slots ("COMBAT_0", "DEFENSIVE_0", "MINING_0", "UNIVERSAL_0")
    // don't — we don't yet know the NBT shape that tells us which gem was
    // actually inserted there (no live sample has shown one populated).
    // Skipping those for now rather than guessing wrong.
    private static string? MapGemToProduct(string slotKey, string quality)
    {
        string slotName = slotKey.Split('_')[0].ToUpperInvariant();

        if (!NamedGemTypes.Contains(slotName))
            return null;

        return $"{quality.ToUpperInvariant()}_{slotName}_GEM";
    }
}

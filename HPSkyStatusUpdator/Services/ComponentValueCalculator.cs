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

        // Temporary — every contribution (or reason it was skipped) gets
        // one line here, written out to a debug file at the end. Safe to
        // rip this whole thing out (this list, every Breakdown(...) call,
        // and the WriteDebugBreakdown method/call) once you don't need
        // it anymore.
        var breakdown = new List<string>();

        if (attributes.Recombobulated)
        {
            var price = _bazaar.GetBuyPrice(RecombobulatorProduct);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
                breakdown.Add($"Recombobulator: +{price.Value:N0}");
            }
            else
            {
                breakdown.Add("Recombobulator: SKIPPED (no bazaar price for RECOMBOBULATOR_3000)");
            }
        }

        if (attributes.HotPotatoCount is > 0)
        {
            int hotPotatoBooks = Math.Min(attributes.HotPotatoCount.Value, 10);
            int fumingPotatoBooks = Math.Max(attributes.HotPotatoCount.Value - 10, 0);

            var hotPrice = _bazaar.GetBuyPrice(HotPotatoBookProduct);
            if (hotPrice.HasValue)
            {
                double hotTotal = hotPotatoBooks * hotPrice.Value;
                total += hotTotal;
                foundAny = true;
                breakdown.Add($"Hot Potato Books x{hotPotatoBooks}: +{hotTotal:N0}");
            }
            else
            {
                breakdown.Add($"Hot Potato Books x{hotPotatoBooks}: SKIPPED (no bazaar price for HOT_POTATO_BOOK)");
            }

            if (fumingPotatoBooks > 0)
            {
                var fumingPrice = _bazaar.GetBuyPrice(FumingPotatoBookProduct);
                if (fumingPrice.HasValue)
                {
                    double fumingTotal = fumingPotatoBooks * fumingPrice.Value;
                    total += fumingTotal;
                    foundAny = true;
                    breakdown.Add($"Fuming Potato Books x{fumingPotatoBooks}: +{fumingTotal:N0}");
                }
                else
                {
                    breakdown.Add($"Fuming Potato Books x{fumingPotatoBooks}: SKIPPED (no bazaar price for FUMING_POTATO_BOOK)");
                }
            }
        }

        if (attributes.Stars is > 0)
        {
            var starCost = CalculateStarCost(itemTag, attributes.Stars.Value, breakdown);

            if (starCost.HasValue)
            {
                total += starCost.Value;
                foundAny = true;
            }
        }

        if (attributes.Gems.Count == 0)
        {
            breakdown.Add("Gems: none socketed");
        }

        foreach (var (slot, quality) in attributes.Gems)
        {
            string? productId = MapGemToProduct(slot, quality);

            if (productId == null)
            {
                breakdown.Add($"Gem {slot}={quality}: SKIPPED (category slot, gem type unknown — see MapGemToProduct)");
                continue;
            }

            var price = _bazaar.GetBuyPrice(productId);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
                breakdown.Add($"Gem {slot}={quality} ({productId}): +{price.Value:N0}");
            }
            else
            {
                breakdown.Add($"Gem {slot}={quality} ({productId}): SKIPPED (no bazaar price)");
            }
        }

        if (attributes.Enchantments.Count == 0)
        {
            breakdown.Add("Enchants: none");
        }

        foreach (var (enchant, level) in attributes.Enchantments)
        {
            string productId = $"ENCHANTMENT_{enchant.ToUpperInvariant()}_{level}";

            var price = _bazaar.GetBuyPrice(productId);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
                breakdown.Add($"Enchant {enchant} {level} ({productId}): +{price.Value:N0}");
            }
            else
            {
                // No price found (not bazaar-tradeable at this level) —
                // skipped silently in the actual total, as agreed. Still
                // shows up in the debug breakdown so it's visible.
                breakdown.Add($"Enchant {enchant} {level} ({productId}): SKIPPED (no bazaar price)");
            }
        }

        WriteDebugBreakdown(itemTag, breakdown, foundAny ? total : (double?)null);

        return foundAny ? total : null;
    }

    // Stars 1-5: per-item essence amount x current essence price for
    // that item's essence type. Stars 6-10 ("master stars"): each one is
    // a specific dungeon-drop item that only trades on the AH — priced
    // from our own recent SaleHistory instead of the bazaar.
    private double? CalculateStarCost(string itemTag, int stars, List<string> breakdown)
    {
        double total = 0;
        bool foundAny = false;

        int essenceStars = Math.Min(stars, 5);

        if (essenceStars > 0)
        {
            if (StarUpgradeCosts.CostsByItem.TryGetValue(
                itemTag.ToUpperInvariant(),
                out StarUpgradeCosts.StarCost? starCost))
            {
                var essencePrice = _bazaar.GetBuyPrice(starCost.EssenceProductId);

                if (essencePrice.HasValue)
                {
                    int essenceUnits = 0;

                    for (int i = 0; i < essenceStars; i++)
                        essenceUnits += starCost.Costs[i];

                    double essenceTotal = essenceUnits * essencePrice.Value;
                    total += essenceTotal;
                    foundAny = true;
                    breakdown.Add(
                        $"Stars 1-{essenceStars} ({starCost.EssenceProductId} x{essenceUnits}): +{essenceTotal:N0}");
                }
                else
                {
                    breakdown.Add(
                        $"Stars 1-{essenceStars}: SKIPPED (no bazaar price for {starCost.EssenceProductId})");
                }
            }
            else
            {
                breakdown.Add($"Stars 1-{essenceStars}: SKIPPED (no cost table entry for {itemTag})");

                if (_warnedMissingStarCost.Add(itemTag))
                {
                    _logger.LogWarning(
                        "No star essence cost entry for {ItemTag} — star value will be undercounted for this item.",
                        itemTag);
                }
            }
        }

        int masterStars = Math.Max(stars - 5, 0);

        for (int i = 0; i < masterStars && i < StarUpgradeCosts.MasterStarTags.Length; i++)
        {
            string masterStarTag = StarUpgradeCosts.MasterStarTags[i];
            var price = GetRecentAhMedian(masterStarTag);

            if (price.HasValue)
            {
                total += price.Value;
                foundAny = true;
                breakdown.Add($"Master star {i + 1} ({masterStarTag}): +{price.Value:N0}");
            }
            else
            {
                breakdown.Add($"Master star {i + 1} ({masterStarTag}): SKIPPED (no recent AH sales in SaleHistory yet)");
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

    // Temporary — appends one block per calculation to a plain text
    // file so you can see exactly what was/wasn't priced without
    // digging through structured logs. Delete this method and its call
    // site above once you don't need it anymore.
    private static void WriteDebugBreakdown(string itemTag, List<string> breakdown, double? total)
    {
        try
        {
            var dataPath =
                Environment.GetEnvironmentVariable("DATA_PATH")
                ?? AppContext.BaseDirectory;

            string debugFile = Path.Combine(dataPath, "component-value-debug.txt");

            var lines = new List<string>
            {
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | {itemTag}"
            };
            lines.AddRange(breakdown.Select(line => "    " + line));
            lines.Add($"    TOTAL: {(total.HasValue ? total.Value.ToString("N0") : "null (nothing priced)")}");
            lines.Add("");

            File.AppendAllLines(debugFile, lines);
        }
        catch
        {
            // Debug-only convenience — never let a logging failure take
            // down a real sale from being recorded.
        }
    }
}

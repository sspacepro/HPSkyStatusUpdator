using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

// Estimates the coin value of the upgrades applied to an item, using
// current bazaar prices. This is an approximation for prediction signal,
// not an exact cost breakdown — missing prices are skipped rather than
// blocking the calculation (per your call).
public class ComponentValueCalculator
{
    private readonly BazaarPriceService _bazaar;

    private const string RecombobulatorProduct = "RECOMBOBULATOR_3000";
    private const string HotPotatoBookProduct = "HOT_POTATO_BOOK";
    private const string FumingPotatoBookProduct = "FUMING_POTATO_BOOK";

    private static readonly string[] NamedGemTypes =
    {
        "RUBY", "AMETHYST", "JADE", "SAPPHIRE", "AMBER", "TOPAZ",
        "JASPER", "OPAL", "ONYX", "AQUAMARINE", "CITRINE", "PERIDOT"
    };

    public ComponentValueCalculator(BazaarPriceService bazaar)
    {
        _bazaar = bazaar;
    }

    public double? Calculate(ItemAttributes attributes)
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
            // First 10 are Hot Potato Books, anything past that is
            // Fuming Potato Books.
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

    // Named slots ("JADE_0") tell us the gem type from the key itself.
    // Category slots ("COMBAT_0", "DEFENSIVE_0", "MINING_0", "UNIVERSAL_0")
    // don't — we don't yet know the NBT shape that tells us which gem was
    // actually inserted there (no live sample has shown one populated).
    // Skipping those for now rather than guessing wrong; send a sample
    // with a category slot filled and this gets filled in.
    private static string? MapGemToProduct(string slotKey, string quality)
    {
        string slotName = slotKey.Split('_')[0].ToUpperInvariant();

        if (!NamedGemTypes.Contains(slotName))
            return null;

        return $"{quality.ToUpperInvariant()}_{slotName}_GEM";
    }
}

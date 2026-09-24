using fNbt;
using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

// Decodes an item's base64 item_bytes into structured attributes. This
// only deals with the item's NBT — auction-envelope fields (uuid, price,
// item_name, item_lore, timestamps) differ between the live auctions
// endpoint and the ended-auctions endpoint, so those are parsed by
// whichever ingestion service is calling this.
public static class ItemDecoder
{
    public static DecodedItem? Decode(string itemBytesBase64)
    {
        NbtCompound root;

        try
        {
            root = NbtDecoder.Decode(itemBytesBase64);
        }
        catch
        {
            return null;
        }

        NbtCompound? item = null;

        foreach (NbtTag child in root.Tags)
        {
            if (child.Name == "i" && child is NbtList list && list.Count > 0)
            {
                item = list[0] as NbtCompound;
                break;
            }
        }

        if (item == null)
            return null;

        var tag = FindCompound(item, "tag");
        if (tag == null)
            return null;

        var extra = FindCompound(tag, "ExtraAttributes");
        if (extra == null)
            return null;

        var attributes = new ItemAttributes();
        var extras = new ItemExtras();

        string? itemId = TryDecodePet(extra, attributes) ?? FindString(extra, "id");

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        attributes.RarityUpgrades = GetInt(extra, "rarity_upgrades");
        attributes.Recombobulated = attributes.RarityUpgrades > 0;
        attributes.HotPotatoCount = GetIntOrNull(extra, "hot_potato_count");
        attributes.Stars = GetIntOrNull(extra, "upgrade_level");

        DecodeEnchantments(extra, attributes);
        DecodeGems(extra, attributes);
        DecodeScrolls(extra, extras);

        return new DecodedItem
        {
            ItemId = itemId,
            Attributes = attributes,
            Extras = extras
        };
    }

    // Pets don't have a stable "id" field like other items — their type
    // lives inside the petInfo JSON string instead. Returns the derived
    // "{TYPE}_PET" item id and populates the pet fields on attributes, or
    // null if this isn't a pet.
    private static string? TryDecodePet(NbtCompound extra, ItemAttributes attributes)
    {
        string? petInfoJson = FindString(extra, "petInfo");

        if (petInfoJson == null)
            return null;

        try
        {
            using JsonDocument petDoc = JsonDocument.Parse(petInfoJson);
            var petRoot = petDoc.RootElement;

            string? petType = petRoot.TryGetProperty("type", out var typeEl)
                ? typeEl.GetString()
                : null;

            if (petType == null)
                return null;

            string? petRarity = petRoot.TryGetProperty("tier", out var tierEl)
                ? tierEl.GetString()
                : null;

            double? petExp = petRoot.TryGetProperty("exp", out var expEl)
                ? expEl.GetDouble()
                : null;

            attributes.PetType = petType;
            attributes.PetRarity = petRarity;
            attributes.PetExp = petExp;

            if (petRarity != null && petExp.HasValue)
            {
                attributes.PetLevel = PetLevelCalculator.XpToLevel(
                    petRarity,
                    (long)petExp.Value);
            }

            return petType + "_PET";
        }
        catch
        {
            // Malformed petInfo — treat as a non-pet rather than failing
            // the whole item.
            return null;
        }
    }

    private static void DecodeEnchantments(NbtCompound extra, ItemAttributes attributes)
    {
        var enchantCompound = FindCompound(extra, "enchantments");

        if (enchantCompound == null)
            return;

        foreach (NbtTag enchantTag in enchantCompound.Tags)
        {
            if (enchantTag.Name != null && enchantTag is NbtInt enchantInt)
                attributes.Enchantments[enchantTag.Name] = enchantInt.Value;
        }
    }

    // Stores the "gems" compound close to as-is. Named slots (e.g.
    // "JADE_0": "PERFECT") tell us the gem type directly from the key.
    // Category slots (COMBAT_0 etc.) are also captured, just not yet
    // resolved to a specific gem — see ComponentValueCalculator, which
    // deliberately skips valuing those until we've confirmed the real
    // shape against a live sample.
    private static void DecodeGems(NbtCompound extra, ItemAttributes attributes)
    {
        var gemsCompound = FindCompound(extra, "gems");

        if (gemsCompound == null)
            return;

        foreach (NbtTag gemTag in gemsCompound.Tags)
        {
            if (gemTag.Name == null)
                continue;

            attributes.Gems[gemTag.Name] = gemTag is NbtString gemString
                ? gemString.Value
                : gemTag.ToString() ?? "";
        }
    }

    private static void DecodeScrolls(NbtCompound extra, ItemExtras extras)
    {
        var scrollList = FindList(extra, "ability_scroll");

        if (scrollList == null)
            return;

        foreach (NbtTag scrollTag in scrollList)
        {
            if (scrollTag is NbtString scrollString)
                extras.Scrolls.Add(scrollString.Value);
        }
    }

    private static NbtCompound? FindCompound(NbtCompound parent, string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name && tag is NbtCompound compound)
                return compound;
        }

        return null;
    }

    private static NbtList? FindList(NbtCompound parent, string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name && tag is NbtList list)
                return list;
        }

        return null;
    }

    private static string? FindString(NbtCompound parent, string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name && tag is NbtString str)
                return str.Value;
        }

        return null;
    }

    private static int GetInt(NbtCompound parent, string name, int defaultValue = 0)
    {
        return GetIntOrNull(parent, name) ?? defaultValue;
    }

    private static int? GetIntOrNull(NbtCompound parent, string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name != name)
                continue;

            return tag switch
            {
                NbtInt i => i.Value,
                NbtShort s => s.Value,
                NbtByte b => b.Value,
                _ => (int?)null
            };
        }

        return null;
    }
}

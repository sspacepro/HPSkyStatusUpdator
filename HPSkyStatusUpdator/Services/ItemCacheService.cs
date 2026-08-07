using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Models;
using System.Globalization;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class ItemCacheService : BackgroundService
{
    private const string ItemsUrl =
        "https://api.hypixel.net/resources/skyblock/items";

    private readonly HttpClient _client;
    private readonly SettingsService _settings;
    private readonly AuctionService _auctions;
    private readonly DatabaseService _database;
    private readonly ServiceHealthService _health;
    private readonly ILogger<ItemCacheService> _logger;


    private volatile List<HypixelItem> _items = new();

    public ItemCacheService(
        HttpClient client,
        SettingsService settings,
        AuctionService auctions,
        DatabaseService database,
        ServiceHealthService health,
        ILogger<ItemCacheService> logger)
    {
        _client = client;
        _settings = settings;
        _auctions = auctions;
        _database = database;
        _health = health;
        _logger = logger;
    }



    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("ItemCacheService");
            bool updated = await Update(stoppingToken);

            // The auction cache may not be ready when the server first starts.
            if (!updated)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);

                continue;
            }

            int minutes = _settings.GetInt(
                SettingKeys.ItemCacheUpdateMinutes,
                1440);

            // Prevent a setting of zero or a negative number
            // from creating a rapid loop.
            minutes = Math.Max(minutes, 1);

            await Task.Delay(
                TimeSpan.FromMinutes(minutes),
                stoppingToken);
        }
    }

    private async Task<bool> Update(
        CancellationToken stoppingToken)
    {
        try
        {
            // Make a stable snapshot because AuctionService may replace
            // its cache while this method is running.
            DecodedAuction[] auctionSnapshot =
                _auctions.GetAllAuctions().ToArray();

            if (auctionSnapshot.Length == 0)
            {
                _logger.LogInformation(
                    "Item cache waiting for auction cache.");

                return false;
            }

            // Every unique item ID currently found on the AH.
            var auctionItems = auctionSnapshot
                .Where(a =>
                    !string.IsNullOrWhiteSpace(a.ItemId))
                .GroupBy(
                    a => a.ItemId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            string json = await _client.GetStringAsync(
                ItemsUrl,
                stoppingToken);

            using JsonDocument document =
                JsonDocument.Parse(json);

            var finalItems =
                new Dictionary<string, HypixelItem>(
                    StringComparer.OrdinalIgnoreCase);

            JsonElement resourceItems =
                document.RootElement.GetProperty("items");

            foreach (JsonElement item in
                     resourceItems.EnumerateArray())
            {
                string id = item.TryGetProperty(
                    "id",
                    out JsonElement idElement)
                        ? idElement.GetString() ?? ""
                        : "";

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Do not add resource items unless they currently
                // have at least one auction listing.
                if (!auctionItems.ContainsKey(id))
                    continue;

                string name = item.TryGetProperty(
                    "name",
                    out JsonElement nameElement)
                        ? nameElement.GetString() ?? FormatItemId(id)
                        : FormatItemId(id);

                string? tier = item.TryGetProperty(
                    "tier",
                    out JsonElement tierElement)
                        ? tierElement.GetString()
                        : null;

                bool? canRecombobulate =
                    item.TryGetProperty(
                        "can_recombobulate",
                        out JsonElement recombElement)
                            ? recombElement.GetBoolean()
                            : null;

                finalItems[id] = new HypixelItem
                {
                    Id = id,
                    Name = name,
                    Tier = tier,
                    CanRecombobulate = canRecombobulate
                };
            }

            // Add auctionable entries missing from the resource endpoint.
            // This automatically adds pets and any other missing IDs.
            foreach (var entry in auctionItems)
            {
                string itemId = entry.Key;

                if (finalItems.ContainsKey(itemId))
                    continue;

                bool isPet = itemId.EndsWith(
                    "_PET",
                    StringComparison.OrdinalIgnoreCase);

                finalItems[itemId] = new HypixelItem
                {
                    Id = itemId,
                    Name = FormatItemId(itemId),

                    // Pets can appear at multiple tiers, so do not assign
                    // a single tier to them.
                    Tier = isPet
                        ? null
                        : NullIfEmpty(entry.Value.Tier),

                    // Pets cannot be recombobulated. For other missing
                    // entries, the value is unknown.
                    CanRecombobulate = isPet
                        ? false
                        : null
                };
            }

            List<HypixelItem> newItems = finalItems.Values
                .OrderBy(item => item.Name)
                .ToList();

            foreach (var item in newItems)
            {
                _database.UpsertKnownAuctionItem(item);
            }

            _items = newItems;
            _logger.LogInformation($"Internal count: {_items.Count}");

            _logger.LogInformation(
                $"Loaded {_items.Count} currently auctionable items.");

            return true;
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Item cache failed: {ex.Message}");

            return false;
        }
    }

    private static string FormatItemId(string itemId)
    {
        string name = itemId;

        if (name.EndsWith(
            "_PET",
            StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        name = name.Replace('_', ' ')
            .ToLowerInvariant();

        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(name);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    public IReadOnlyList<HypixelItem> GetItems()
    {
        return _database.GetKnownAuctionItems();
    }
    public int GetItemCount()
    {
        return _items.Count;
    }
}
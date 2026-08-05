using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class ItemCacheService : BackgroundService
{
    private readonly HttpClient _client;
    private readonly SettingsService _settings;

    private List<HypixelItem> _items = new();

    public ItemCacheService(
        HttpClient client,
        SettingsService settings)
    {
        _client = client;
        _settings = settings;
    }


    public IReadOnlyList<HypixelItem> GetItems()
    {
        return _items;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Update();

            int minutes = _settings.GetInt(
                SettingKeys.ItemCacheUpdateMinutes,
                1440);

            await Task.Delay(
                TimeSpan.FromMinutes(minutes),
                stoppingToken);
        }
    }


    private async Task Update()
    {
        try
        {
            var json = await _client.GetStringAsync(
                "https://api.hypixel.net/resources/skyblock/items");


            using var doc = JsonDocument.Parse(json);


            var items = new List<HypixelItem>();

            foreach (var item in doc.RootElement
    .GetProperty("items")
    .EnumerateArray())
            {
                string id =
                    item.GetProperty("id")
                    .GetString() ?? "";


                string name =
                    item.GetProperty("name")
                    .GetString() ?? "";


                string tier =
                    item.TryGetProperty("tier", out var t)
                    ? t.GetString() ?? ""
                    : "";


                bool? canRecombobulate = null;

                if (item.TryGetProperty(
                    "can_recombobulate",
                    out var recomb))
                {
                    canRecombobulate = recomb.GetBoolean();
                }





                items.Add(new HypixelItem
                {
                    Id = id,
                    Name = name,
                    Tier = tier,
                    CanRecombobulate = canRecombobulate
                });
            }


            _items = items
            .Concat(AuctionItems.CustomItems)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();


            Console.WriteLine(
                $"Loaded {_items.Count} Hypixel items.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Item cache failed: {ex.Message}");
        }
    }
}
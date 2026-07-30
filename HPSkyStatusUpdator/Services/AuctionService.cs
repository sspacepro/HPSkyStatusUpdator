using fNbt;
using HPSkyStatusUpdator.Models;
using System.IO.Compression;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class AuctionService
{
    private readonly HttpClient _client;
    private volatile List<DecodedAuction> _cache = new();

    private volatile Dictionary<string, List<DecodedAuction>> _auctionIndex =
    new(StringComparer.OrdinalIgnoreCase);


    private const string AuctionUrl =
        "https://api.hypixel.net/v2/skyblock/auctions";

    private DateTime _cacheTime = DateTime.MinValue;


    private readonly TimeSpan _cacheDuration =
        TimeSpan.FromMinutes(1);
    public AuctionService(HttpClient client)
    {
        _client = client;
    }

    public IReadOnlyList<DecodedAuction> GetAllAuctions()
    {
        return _cache;
    }

    public IReadOnlyList<DecodedAuction> GetAuctions(string itemId)
    {
        if (_auctionIndex.TryGetValue(itemId, out var auctions))
            return auctions;

        return Array.Empty<DecodedAuction>();
    }

    private async Task<List<DecodedAuction>> DownloadAuctions()
    {
        var decodedAuctions = new List<DecodedAuction>();


        // Get first page to find total pages
        JsonDocument? firstPage =
            await GetPage(0);

        if (firstPage == null)
            return decodedAuctions;


        JsonElement root = firstPage.RootElement;


        if (!root.TryGetProperty(
                "totalPages",
                out var totalPagesElement))
        {
            return decodedAuctions;
        }


        int totalPages =
            totalPagesElement.GetInt32();

        Console.WriteLine(
            $"Downloading {totalPages} auction pages"
        );

        var semaphore = new SemaphoreSlim(10);

        var pageTasks = Enumerable.Range(1, totalPages)
            .Select(async page =>
            {
                await semaphore.WaitAsync();

                try
                {
                    return await GetPage(page);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToArray();


        JsonDocument?[] otherPages =
            await Task.WhenAll(pageTasks);

        ProcessPage(firstPage, decodedAuctions);

        foreach (var page in otherPages)
        {
            if (page != null)
            {
                ProcessPage(page, decodedAuctions);
            }
        }

        return decodedAuctions;
    }



    private async Task<JsonDocument?> GetPage(int page)
    {
        try
        {
            using var response =
                await _client.GetAsync(
                    $"{AuctionUrl}?page={page}"
                );


            if (!response.IsSuccessStatusCode)
                return null;


            string json =
                await response.Content.ReadAsStringAsync();


            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Auction page error {page}: {ex.Message}"
            );

            return null;
        }
    }

    public async Task RefreshCache()
    {
        Console.WriteLine("Refreshing auction cache...");

        var newCache = await DownloadAuctions();

        var newIndex = newCache
            .GroupBy(a => a.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        // Swap everything at once
        _cache = newCache;
        _auctionIndex = newIndex;




        _cacheTime = DateTime.UtcNow;


        Console.WriteLine(
            $"Auction cache updated: {_cache.Count} auctions"
        );
    }





    private NbtCompound? GetExtraAttributes(JsonElement auction)
    {
        if (!auction.TryGetProperty("item_bytes", out var bytes))
            return null;

        string itemBytes = bytes.GetString()!;

        byte[] compressed = Convert.FromBase64String(itemBytes);

        using var compressedStream = new MemoryStream(compressed);
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);

        NbtFile file = new();
        file.LoadFromStream(gzip, NbtCompression.None);

        var root = file.RootTag;

        NbtCompound? item = null;

        foreach (NbtTag child in root.Tags)
        {
            if (child.Name == "i" && child is NbtList list)
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

        return FindCompound(tag, "ExtraAttributes");
    }

    private string? GetItemId(NbtCompound extra)
    {
        var petInfo = FindString(extra, "petInfo");

        if (petInfo != null)
        {
            using JsonDocument petDoc = JsonDocument.Parse(petInfo);

            if (petDoc.RootElement.TryGetProperty("type", out var type))
                return type.GetString() + "_PET";
        }

        return FindString(extra, "id");
    }

    private NbtCompound? FindCompound(
        NbtCompound parent,
        string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name &&
                tag is NbtCompound compound)
            {
                return compound;
            }
        }

        return null;
    }



    private string? FindString(
        NbtCompound parent,
        string name)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name == name &&
                tag is NbtString str)
            {
                return str.Value;
            }
        }

        return null;
    }

    private int GetInt(
    NbtCompound parent,
    string name,
    int defaultValue = 0)
    {
        foreach (NbtTag tag in parent.Tags)
        {
            if (tag.Name != name)
                continue;

            if (tag is NbtInt i)
                return i.Value;

            if (tag is NbtShort s)
                return s.Value;

            if (tag is NbtByte b)
                return b.Value;
        }

        return defaultValue;
    }

    private long? GetPetXp(NbtCompound extra)
    {
        string? petInfo = FindString(extra, "petInfo");

        if (petInfo == null)
            return null;

        using JsonDocument pet =
            JsonDocument.Parse(petInfo);

        if (!pet.RootElement.TryGetProperty(
                "exp",
                out var xp))
        {
            return null;
        }

        return (long)xp.GetDouble();
    }

    private void ProcessPage(
    JsonDocument document,
    List<DecodedAuction> decodedAuctions)
    {
        if (!document.RootElement.TryGetProperty(
                "auctions",
                out var auctions))
        {
            return;
        }

        foreach (var auction in auctions.EnumerateArray())
        {
            // Ignore anything without BIN
            if (!auction.TryGetProperty(
                    "bin",
                    out var bin))
            {
                continue;
            }


            if (!bin.GetBoolean())
                continue;






            long price =
                auction.GetProperty(
                    "starting_bid")
                .GetInt64();


            var extra = GetExtraAttributes(auction);

            if (extra == null)
                continue;

            string? itemId = GetItemId(extra);

            if (itemId == null)
                continue;


            var decoded = new DecodedAuction
            {
                Uuid = auction.GetProperty("uuid").GetString() ?? "",

                ItemId = itemId,

                ItemName = auction.GetProperty("item_name").GetString() ?? "",

                Tier = auction.GetProperty("tier").GetString() ?? "",

                Price = price,

                Stars = GetInt(extra, "upgrade_level"),

                Recombobulated = GetInt(extra, "rarity_upgrades") > 0,

                PetXp = GetPetXp(extra),

                ItemLore = auction.GetProperty("item_lore").GetString() ?? "",
                DisplayItemName = auction.GetProperty("item_name").GetString() ?? ""
            };


            decodedAuctions.Add(decoded);

        }
    
    }


    public IReadOnlyList<DecodedAuction> SearchAuctions(AuctionSearch search)
    {
        if (!_auctionIndex.TryGetValue(search.ItemTag, out var auctions))
            return Array.Empty<DecodedAuction>();

        return auctions
            .Where(a =>
                search.Tier == null ||
                a.Tier.Equals(
                    search.Tier,
                    StringComparison.OrdinalIgnoreCase))

            .Where(a =>
                search.Stars == null ||
                a.Stars == search.Stars)

            .Where(a =>
                search.Recombobulated == null ||
                a.Recombobulated == search.Recombobulated)

            .ToList();
    }
}

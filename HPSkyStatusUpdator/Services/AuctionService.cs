using System.Text.Json;
using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

public class AuctionService
{
    private readonly HttpClient _client;

    public AuctionService(HttpClient client)
    {
        _client = client;
    }

    public async Task<AuctionResult?> GetLowestBin(AuctionSearch search)
    {
        using var response =
            await _client.GetAsync(
                $"https://sky.coflnet.com/api/auctions/tag/{search.ItemTag}/active/bin"
            );

        if (!response.IsSuccessStatusCode)
            return null;

        string json =
            await response.Content.ReadAsStringAsync();

        using JsonDocument doc =
            JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;


        AuctionResult? lowest = null;


        foreach (var auction in doc.RootElement.EnumerateArray())
        {
            if (!MatchesWatch(auction, search))
                continue;


            long price =
                auction.GetProperty("startingBid")
                       .GetInt64();


            if (lowest == null || price < lowest.LowestBin)
            {
                lowest = new AuctionResult
                {
                    ItemTag = search.ItemTag,

                    ItemName =
                        auction.GetProperty("itemName")
                               .GetString() ?? search.ItemTag,

                    LowestBin = price,

                    Tier =
                        auction.GetProperty("tier")
                               .GetString() ?? "",

                    Stars =
                        GetStars(auction),

                    Recombobulated =
                        IsRecombobulated(auction)
                };
            }
        }


        return lowest;
    }
    private bool MatchesWatch(
    JsonElement auction,
    AuctionSearch search)
    {
        if (search.PetXp.HasValue)
        {
            if (!auction.TryGetProperty(
                "petInfo",
                out var petInfo))
                return false;


            if (!petInfo.TryGetProperty(
                "exp",
                out var xp))
                return false;


            if (xp.GetInt64() < search.PetXp.Value)
                return false;
        }

        if (search.Tier != null)
        {
            string tier =
                auction.GetProperty("tier")
                       .GetString() ?? "";

            if (!tier.Equals(
                    search.Tier,
                    StringComparison.OrdinalIgnoreCase))
                return false;
        }


        if (search.Stars.HasValue)
        {
            if (GetStars(auction) != search.Stars.Value)
                return false;
        }


        if (search.Recombobulated.HasValue)
        {
            if (IsRecombobulated(auction) != search.Recombobulated.Value)
                return false;
        }


        return true;
    }
    private bool IsRecombobulated(JsonElement auction)
    {
        if (!auction.TryGetProperty(
                "nbtData",
                out var nbt))
            return false;


        if (!nbt.TryGetProperty(
                "data",
                out var data))
            return false;


        return data.TryGetProperty(
            "rarity_upgrades",
            out var upgrades)
            && upgrades.GetInt32() > 0;
    }
    private int GetStars(JsonElement auction)
    {
        if (auction.TryGetProperty("nbtData", out var nbt) &&
            nbt.TryGetProperty("data", out var data) &&
            data.TryGetProperty("upgrade_level", out var stars))
        {
            return stars.GetInt32();
        }

        return 0;
    }
}
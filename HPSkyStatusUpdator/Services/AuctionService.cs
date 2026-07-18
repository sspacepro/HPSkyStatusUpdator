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

    public async Task<AuctionResult?> GetLowestBin(AuctionWatch watch)
    {
        using var response =
            await _client.GetAsync(
                $"https://sky.coflnet.com/api/auctions/tag/{watch.ItemTag}/active/bin"
            );

        if (!response.IsSuccessStatusCode)
            return null;

        string json =
            await response.Content.ReadAsStringAsync();

        using JsonDocument doc =
            JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        if (doc.RootElement.GetArrayLength() == 0)
            return null;

        var auction = doc.RootElement[0];

        return new AuctionResult
        {
            ItemTag = watch.ItemTag,

            ItemName =
                auction.GetProperty("itemName")
                       .GetString() ?? watch.ItemTag,

            LowestBin =
                auction.GetProperty("startingBid")
                       .GetInt64()
        };
    }
}
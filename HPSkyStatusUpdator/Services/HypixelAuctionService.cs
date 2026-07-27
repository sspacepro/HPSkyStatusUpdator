using System.Text.Json;
using HPSkyStatusUpdator.Models;


namespace HPSkyStatusUpdator.Services;

public class HypixelAuctionService
{
    private readonly HttpClient _http;

    public HypixelAuctionService(HttpClient http)
    {
        _http = http;
    }


    public async Task<List<HypixelAuction>> GetPage(int page)
    {
        string url =
            $"https://api.hypixel.net/v2/skyblock/auctions?page={page}";


        var json =
            await _http.GetStringAsync(url);


        using JsonDocument doc =
            JsonDocument.Parse(json);


        var auctions =
            new List<HypixelAuction>();


        foreach (var item in doc.RootElement
            .GetProperty("auctions")
            .EnumerateArray())
        {

            // Ignore non BIN auctions
            if (!item.TryGetProperty("bin", out var bin))
                continue;


            if (!bin.GetBoolean())
                continue;


            auctions.Add(new HypixelAuction
            {
                Json = item.Clone()
            });
        }


        return auctions;
    }
}
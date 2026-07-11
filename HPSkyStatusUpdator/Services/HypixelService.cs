using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class HypixelService
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;

    private int _skyblockPlayers = -1;

    public HypixelService(HttpClient client, IConfiguration config)
    {
        _client = client;
        _config = config;
    }


    public int GetSkyblockPlayers()
    {
        return _skyblockPlayers;
    }


    public async Task Update()
    {
        try
        {
            string apiKey = _config["Hypixel:ApiKey"]!;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.hypixel.net/v2/counts"
            );

            request.Headers.Add("API-Key", apiKey);

            using var response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Hypixel request failed");
                return;
            }


            string json = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(json);

            _skyblockPlayers = doc.RootElement
                .GetProperty("games")
                .GetProperty("SKYBLOCK")
                .GetProperty("players")
                .GetInt32();


            Console.WriteLine(
                $"SkyBlock players: {_skyblockPlayers}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
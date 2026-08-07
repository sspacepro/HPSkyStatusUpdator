using HPSkyStatusUpdator.Configuration;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class HypixelService
{
    private readonly HttpClient _client;
    private readonly SettingsService _settings;
    private readonly ILogger<HypixelService> _logger;

    private int _skyblockPlayers = -1;

    public HypixelService(HttpClient client, SettingsService settings, ILogger<HypixelService> logger)
    {
        _client = client;
        _settings = settings;
        _logger = logger;
    }


    public int GetSkyblockPlayers()
    {
        return _skyblockPlayers;
    }


    public async Task Update()
    {
        try
        {
            string? apiKey = _settings.GetString(
                SettingKeys.HypixelApiKey
            );

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _skyblockPlayers = -1;
                _logger.LogInformation("Hypixel API key not configured.");
                return;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.hypixel.net/v2/counts"
            );

            request.Headers.Add("API-Key", apiKey);

            using var response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _skyblockPlayers = -1;
                _logger.LogInformation($"Hypixel request failed: {(int)response.StatusCode}");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(json);

            _skyblockPlayers = doc.RootElement
                .GetProperty("games")
                .GetProperty("SKYBLOCK")
                .GetProperty("players")
                .GetInt32();

            _logger.LogInformation(
                $"SkyBlock players: {_skyblockPlayers}"
            );
        }
        catch (Exception ex)
        {
            _skyblockPlayers = -1;
            _logger.LogError(ex, "Error occurred while updating Hypixel player count.");
        }
    }
}
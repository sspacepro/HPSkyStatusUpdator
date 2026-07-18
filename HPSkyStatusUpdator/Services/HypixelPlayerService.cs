using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;


public class HypixelPlayerService
{
    private readonly HttpClient _client;
    private readonly SettingsService _settings;

    public HypixelPlayerService(
        HttpClient client,
        SettingsService settings)
    {
        _client = client;
        _settings = settings;
    }


    public async Task<HypixelStatus> GetStatus(string username)
    {
        string? uuid = await GetUuid(username);

        if (uuid == null)
            return new HypixelStatus();

        return await GetStatusByUuid(uuid);
    }

    public async Task<HypixelStatus> GetStatusByUuid(string uuid)
    {
        string apiKey = _settings.GetString("HypixelApiKey") ?? "";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.hypixel.net/v2/status?uuid={uuid}"
        );

        request.Headers.Add("API-Key", apiKey);

        using var response = await _client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return new HypixelStatus();

        string json = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        var session = doc.RootElement.GetProperty("session");

        return new HypixelStatus
        {
            Online = session.GetProperty("online").GetBoolean(),
            GameType = session.TryGetProperty("gameType", out var game)
                ? game.GetString() ?? ""
                : "",
            Mode = session.TryGetProperty("mode", out var mode)
                ? mode.GetString() ?? ""
                : ""
        };
    }
    public async Task<string?> GetUuid(string username)
    {
        using var response =
            await _client.GetAsync(
                $"https://api.mojang.com/users/profiles/minecraft/{username}"
            );


        if (!response.IsSuccessStatusCode)
            return null;


        string json =
            await response.Content.ReadAsStringAsync();


        using JsonDocument doc =
            JsonDocument.Parse(json);


        return doc.RootElement
            .GetProperty("id")
            .GetString();
    }


}
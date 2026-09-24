using HPSkyStatusUpdator.Configuration;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class BazaarPriceService : BackgroundService
{
    private const string BazaarUrl = "https://api.hypixel.net/v2/skyblock/bazaar";

    private readonly HttpClient _client;
    private readonly MarketDatabaseService _market;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    private readonly ILogger<BazaarPriceService> _logger;

    public BazaarPriceService(
        HttpClient client,
        MarketDatabaseService market,
        SettingsService settings,
        ServiceHealthService health,
        ILogger<BazaarPriceService> logger)
    {
        _client = client;
        _market = market;
        _settings = settings;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("BazaarPriceService");

            try
            {
                await RefreshPrices(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bazaar refresh failed");
            }

            // NOTE: add BazaarRefreshMinutes to SettingKeys (default 60)
            int minutes = _settings.GetInt(SettingKeys.BazaarRefreshMinutes, 60);
            minutes = Math.Max(minutes, 5);

            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }

    private async Task RefreshPrices(CancellationToken stoppingToken)
    {
        string json = await _client.GetStringAsync(BazaarUrl, stoppingToken);

        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("products", out var products))
            return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using var connection = _market.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        int count = 0;

        foreach (var product in products.EnumerateObject())
        {
            if (!product.Value.TryGetProperty("quick_status", out var status))
                continue;

            double buyPrice = status.TryGetProperty("buyPrice", out var buyEl)
                ? buyEl.GetDouble()
                : 0;

            double sellPrice = status.TryGetProperty("sellPrice", out var sellEl)
                ? sellEl.GetDouble()
                : 0;

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            """
            INSERT INTO BazaarPrices (ProductId, BuyPrice, SellPrice, CapturedAt)
            VALUES ($id, $buy, $sell, $captured)
            ON CONFLICT(ProductId) DO UPDATE SET
                BuyPrice = excluded.BuyPrice,
                SellPrice = excluded.SellPrice,
                CapturedAt = excluded.CapturedAt;
            """;

            command.Parameters.AddWithValue("$id", product.Name);
            command.Parameters.AddWithValue("$buy", buyPrice);
            command.Parameters.AddWithValue("$sell", sellPrice);
            command.Parameters.AddWithValue("$captured", now);

            command.ExecuteNonQuery();
            count++;
        }

        transaction.Commit();

        _logger.LogInformation("Bazaar prices refreshed: {Count} products.", count);
    }

    // Buy price is used as the component cost — it's what you'd actually
    // pay to acquire the item right now.
    public double? GetBuyPrice(string productId)
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT BuyPrice FROM BazaarPrices WHERE ProductId = $id;";
        command.Parameters.AddWithValue("$id", productId);

        var result = command.ExecuteScalar();

        return result == null || result is DBNull
            ? null
            : Convert.ToDouble(result);
    }
}

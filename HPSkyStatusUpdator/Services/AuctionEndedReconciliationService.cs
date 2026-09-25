using HPSkyStatusUpdator.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class AuctionEndedReconciliationService : BackgroundService
{
    private const string EndedUrl = "https://api.hypixel.net/v2/skyblock/auctions_ended";

    private readonly HttpClient _client;
    private readonly MarketDatabaseService _market;
    private readonly ComponentValueCalculator _valueCalculator;
    private readonly OutlierDetector _outliers;
    private readonly ServiceHealthService _health;
    private readonly ILogger<AuctionEndedReconciliationService> _logger;

    public AuctionEndedReconciliationService(
        HttpClient client,
        MarketDatabaseService market,
        ComponentValueCalculator valueCalculator,
        OutlierDetector outliers,
        ServiceHealthService health,
        ILogger<AuctionEndedReconciliationService> logger)
    {
        _client = client;
        _market = market;
        _valueCalculator = valueCalculator;
        _outliers = outliers;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("AuctionEndedReconciliationService");

            try
            {
                await ReconcileOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ended-auction reconciliation failed");
            }

            // The ended feed itself only refreshes about once a minute.
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task ReconcileOnce(CancellationToken stoppingToken)
    {
        string json = await _client.GetStringAsync(EndedUrl, stoppingToken);
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("auctions", out var auctions))
            return;

        int sold = 0;

        using var connection = _market.GetConnection();
        connection.Open();

        foreach (var raw in auctions.EnumerateArray())
        {
            string auctionId = raw.TryGetProperty("auction_id", out var idEl)
                ? idEl.GetString() ?? ""
                : "";

            if (string.IsNullOrEmpty(auctionId))
                continue;

            if (!raw.TryGetProperty("bin", out var binEl) || !binEl.GetBoolean())
                continue; // BIN only, per current scope

            long price = raw.TryGetProperty("price", out var priceEl)
                ? priceEl.GetInt64()
                : 0;

            long soldAt = raw.TryGetProperty("timestamp", out var tsEl)
                ? tsEl.GetInt64()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var existing = GetAuction(connection, auctionId);

            // We never saw this one as ACTIVE (missed page, restart,
            // etc.) — nothing to reconcile against. The full sweep
            // (next pass) is the backstop for anything genuinely missed.
            if (existing == null)
                continue;

            RecordSale(connection, auctionId, existing.Value, price, soldAt);
            MarkSold(connection, auctionId);

            sold++;
        }

        _logger.LogInformation("Ended-auction reconciliation: {Count} sales recorded.", sold);
    }

    private static (string ItemTag, string Tier, string Attributes)? GetAuction(
        SqliteConnection connection,
        string uuid)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ItemTag, Tier, Attributes FROM Auctions WHERE Uuid = $uuid;";
        command.Parameters.AddWithValue("$uuid", uuid);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return (
            reader.GetString(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.GetString(2));
    }

    private void RecordSale(
        SqliteConnection connection,
        string uuid,
        (string ItemTag, string Tier, string Attributes) existing,
        long price,
        long soldAt)
    {
        var attributes = JsonSerializer.Deserialize<ItemAttributes>(existing.Attributes)
            ?? new ItemAttributes();

        bool isOutlier = _outliers.IsOutlier(connection, existing.ItemTag, price);
        double? componentValue = _valueCalculator.Calculate(attributes);

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO SaleHistory
        (
            Uuid, ItemTag, Tier, FinalPrice, SoldAt,
            Stars, RarityUpgrades, HotPotatoCount, PetLevel,
            ComponentValue, Extras, IsOutlier
        )
        VALUES
        (
            $uuid, $itemTag, $tier, $price, $soldAt,
            $stars, $rarityUpgrades, $hotPotato, $petLevel,
            $componentValue, $extras, $isOutlier
        )
        ON CONFLICT(Uuid) DO NOTHING;
        """;

        command.Parameters.AddWithValue("$uuid", uuid);
        command.Parameters.AddWithValue("$itemTag", existing.ItemTag);
        command.Parameters.AddWithValue("$tier", existing.Tier);
        command.Parameters.AddWithValue("$price", price);
        command.Parameters.AddWithValue("$soldAt", soldAt);
        command.Parameters.AddWithValue("$stars", (object?)attributes.Stars ?? DBNull.Value);
        command.Parameters.AddWithValue("$rarityUpgrades", attributes.RarityUpgrades);
        command.Parameters.AddWithValue("$hotPotato", (object?)attributes.HotPotatoCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$petLevel", (object?)attributes.PetLevel ?? DBNull.Value);
        command.Parameters.AddWithValue("$componentValue", (object?)componentValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$extras", "{}");
        command.Parameters.AddWithValue("$isOutlier", isOutlier ? 1 : 0);

        command.ExecuteNonQuery();
    }

    private static void MarkSold(SqliteConnection connection, string uuid)
    {
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Auctions WHERE Uuid = $uuid;";
        command.Parameters.AddWithValue("$uuid", uuid);
        command.ExecuteNonQuery();
    }
}

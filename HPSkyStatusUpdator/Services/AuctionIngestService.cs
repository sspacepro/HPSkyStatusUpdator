using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

public class AuctionIngestService : BackgroundService
{
    private const string AuctionUrl = "https://api.hypixel.net/v2/skyblock/auctions";

    private readonly HttpClient _client;
    private readonly MarketDatabaseService _market;
    private readonly BlacklistService _blacklist;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    private readonly ILogger<AuctionIngestService> _logger;

    // Auction uuids seen last cycle. Since the API returns auctions
    // sorted by last-updated-first, hitting one of these means everything
    // after it (this page onward) is already in our table.
    private HashSet<string> _lastKnownUuids = new();

    public AuctionIngestService(
        HttpClient client,
        MarketDatabaseService market,
        BlacklistService blacklist,
        SettingsService settings,
        ServiceHealthService health,
        ILogger<AuctionIngestService> logger)
    {
        _client = client;
        _market = market;
        _blacklist = blacklist;
        _settings = settings;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("AuctionIngestService");

            try
            {
                await IngestOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Incremental auction ingest failed");
            }

            // NOTE: add AuctionIngestIntervalSeconds to SettingKeys (default 60)
            int seconds = _settings.GetInt(SettingKeys.AuctionIngestIntervalSeconds, 60);

            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    private async Task IngestOnce(CancellationToken stoppingToken)
    {
        var seenThisRun = new HashSet<string>();
        var toUpsert = new List<(DecodedAuction Auction, string ItemTag)>();

        int page = 0;
        int totalPages = 1;

        while (page < totalPages)
        {
            using var response = await _client.GetAsync(
                $"{AuctionUrl}?page={page}",
                stoppingToken);

            if (!response.IsSuccessStatusCode)
                break;

            string json = await response.Content.ReadAsStringAsync(stoppingToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (page == 0 && root.TryGetProperty("totalPages", out var totalPagesEl))
                totalPages = totalPagesEl.GetInt32();

            if (!root.TryGetProperty("auctions", out var auctions))
                break;

            bool hitKnownAuction = false;

            foreach (var raw in auctions.EnumerateArray())
            {
                string uuid = raw.TryGetProperty("uuid", out var uuidEl)
                    ? uuidEl.GetString() ?? ""
                    : "";

                if (string.IsNullOrEmpty(uuid))
                    continue;

                seenThisRun.Add(uuid);

                if (_lastKnownUuids.Contains(uuid))
                {
                    hitKnownAuction = true;
                    break;
                }

                if (!raw.TryGetProperty("bin", out var binEl) || !binEl.GetBoolean())
                    continue; // BIN only, per current scope

                var parsed = ParseAuction(raw);

                if (parsed == null)
                    continue;

                var (auction, itemTag) = parsed.Value;

                if (_blacklist.IsBlacklisted(itemTag))
                    continue;

                toUpsert.Add((auction, itemTag));
            }

            if (hitKnownAuction)
                break;

            page++;
        }

        if (toUpsert.Count > 0)
            UpsertAuctions(toUpsert);

        _lastKnownUuids = seenThisRun;

        _logger.LogInformation(
            "Auction ingest: {Count} new/updated BIN auctions this cycle.",
            toUpsert.Count);
    }

    private static (DecodedAuction Auction, string ItemTag)? ParseAuction(JsonElement raw)
    {
        string itemBytes = raw.TryGetProperty("item_bytes", out var bytesEl)
            ? bytesEl.GetString() ?? ""
            : "";

        if (string.IsNullOrEmpty(itemBytes))
            return null;

        var decoded = ItemDecoder.Decode(itemBytes);

        if (decoded == null)
            return null;

        var auction = new DecodedAuction
        {
            Uuid = raw.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "",
            ItemId = decoded.ItemId,
            ItemName = raw.TryGetProperty("item_name", out var n) ? n.GetString() ?? "" : "",
            Tier = raw.TryGetProperty("tier", out var t) ? t.GetString() ?? "" : "",
            Price = raw.TryGetProperty("starting_bid", out var p) ? p.GetInt64() : 0,
            Start = raw.TryGetProperty("start", out var s) ? s.GetInt64() : 0,
            End = raw.TryGetProperty("end", out var e) ? e.GetInt64() : 0,
            DisplayItemName = raw.TryGetProperty("item_name", out var dn) ? dn.GetString() ?? "" : "",
            ItemLore = raw.TryGetProperty("item_lore", out var l) ? l.GetString() ?? "" : "",
            Attributes = decoded.Attributes,
            Extras = decoded.Extras
        };

        if (string.IsNullOrEmpty(auction.Uuid))
            return null;

        return (auction, decoded.ItemId);
    }

    private void UpsertAuctions(List<(DecodedAuction Auction, string ItemTag)> items)
    {
        using var connection = _market.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (auction, itemTag) in items)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            """
            INSERT INTO Auctions
            (
                Uuid, ItemTag, Tier, Price, DisplayItemName, ItemLore,
                Attributes, Extras, StartTime, EndTime, LastSeenAt, State
            )
            VALUES
            (
                $uuid, $itemTag, $tier, $price, $displayName, $lore,
                $attributes, $extras, $start, $end, $lastSeen, 'ACTIVE'
            )
            ON CONFLICT(Uuid) DO UPDATE SET
                Price = excluded.Price,
                LastSeenAt = excluded.LastSeenAt,
                State = 'ACTIVE';
            """;

            command.Parameters.AddWithValue("$uuid", auction.Uuid);
            command.Parameters.AddWithValue("$itemTag", itemTag);
            command.Parameters.AddWithValue("$tier", auction.Tier);
            command.Parameters.AddWithValue("$price", auction.Price);
            command.Parameters.AddWithValue("$displayName", auction.DisplayItemName);
            command.Parameters.AddWithValue("$lore", auction.ItemLore);
            command.Parameters.AddWithValue("$attributes", JsonSerializer.Serialize(auction.Attributes));
            command.Parameters.AddWithValue("$extras", JsonSerializer.Serialize(auction.Extras));
            command.Parameters.AddWithValue("$start", auction.Start);
            command.Parameters.AddWithValue("$end", auction.End);
            command.Parameters.AddWithValue("$lastSeen", now);

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}

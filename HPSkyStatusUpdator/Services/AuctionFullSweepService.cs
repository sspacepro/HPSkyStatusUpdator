using HPSkyStatusUpdator.Configuration;
using HPSkyStatusUpdator.Models;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

// Backstop for AuctionIngestService: pulls every page on a much longer
// interval to (a) catch anything a transient error or restart caused the
// incremental fetch to miss, and (b) mark auctions that vanished from
// the AH without ever appearing in the ended-auctions feed as EXPIRED —
// i.e. no buyer, nothing to record as a sale.
public class AuctionFullSweepService : BackgroundService
{
    private const string AuctionUrl = "https://api.hypixel.net/v2/skyblock/auctions";

    private readonly HttpClient _client;
    private readonly MarketDatabaseService _market;
    private readonly BlacklistService _blacklist;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    private readonly ILogger<AuctionFullSweepService> _logger;

    public AuctionFullSweepService(
        HttpClient client,
        MarketDatabaseService market,
        BlacklistService blacklist,
        SettingsService settings,
        ServiceHealthService health,
        ILogger<AuctionFullSweepService> logger)
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
            _health.Beat("AuctionFullSweepService");

            try
            {
                await SweepOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Full auction sweep failed");
            }

            // NOTE: add FullSweepIntervalMinutes to SettingKeys (default 60)
            int minutes = _settings.GetInt(SettingKeys.FullSweepIntervalMinutes, 60);
            minutes = Math.Max(minutes, 15);

            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }

    private async Task SweepOnce(CancellationToken stoppingToken)
    {
        JsonDocument? firstPage = await GetPage(0, stoppingToken);

        if (firstPage == null)
            return;

        if (!firstPage.RootElement.TryGetProperty("totalPages", out var totalPagesEl))
        {
            firstPage.Dispose();
            return;
        }

        int totalPages = totalPagesEl.GetInt32();

        _logger.LogInformation("Full sweep: downloading {Count} pages.", totalPages);

        var semaphore = new SemaphoreSlim(10);

        var pageTasks = Enumerable.Range(1, Math.Max(totalPages - 1, 0))
            .Select(async page =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    return await GetPage(page, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToArray();

        JsonDocument?[] otherPages = await Task.WhenAll(pageTasks);

        var seenUuids = new HashSet<string>();
        var toUpsert = new List<(DecodedAuction Auction, string ItemTag)>();

        ProcessPage(firstPage, seenUuids, toUpsert);
        firstPage.Dispose();

        foreach (var page in otherPages)
        {
            if (page == null)
                continue;

            ProcessPage(page, seenUuids, toUpsert);
            page.Dispose();
        }

        if (toUpsert.Count > 0)
            Upsert(toUpsert);

        int expired = MarkExpiredMissing(seenUuids);

        _logger.LogInformation(
            "Full sweep complete: {Upserted} upserted, {Expired} marked expired.",
            toUpsert.Count,
            expired);
    }

    private void ProcessPage(
        JsonDocument document,
        HashSet<string> seenUuids,
        List<(DecodedAuction Auction, string ItemTag)> toUpsert)
    {
        if (!document.RootElement.TryGetProperty("auctions", out var auctions))
            return;

        foreach (var raw in auctions.EnumerateArray())
        {
            string uuid = raw.TryGetProperty("uuid", out var uuidEl)
                ? uuidEl.GetString() ?? ""
                : "";

            if (string.IsNullOrEmpty(uuid))
                continue;

            // BIN only, per current scope — non-BIN auctions are never
            // written to our table, so excluding them here too keeps
            // MarkExpiredMissing from touching rows it doesn't own.
            if (!raw.TryGetProperty("bin", out var binEl) || !binEl.GetBoolean())
                continue;

            seenUuids.Add(uuid);

            var itemBytes = raw.TryGetProperty("item_bytes", out var bytesEl)
                ? bytesEl.GetString() ?? ""
                : "";

            if (string.IsNullOrEmpty(itemBytes))
                continue;

            var decoded = ItemDecoder.Decode(itemBytes);

            if (decoded == null)
                continue;

            if (_blacklist.IsBlacklisted(decoded.ItemId))
                continue;

            var auction = new DecodedAuction
            {
                Uuid = uuid,
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

            toUpsert.Add((auction, decoded.ItemId));
        }
    }

    private async Task<JsonDocument?> GetPage(int page, CancellationToken stoppingToken)
    {
        try
        {
            using var response = await _client.GetAsync(
                $"{AuctionUrl}?page={page}",
                stoppingToken);

            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync(stoppingToken);

            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sweep page error {Page}", page);
            return null;
        }
    }

    private void Upsert(List<(DecodedAuction Auction, string ItemTag)> items)
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
                Attributes, Extras, StartTime, EndTime, LastSeenAt
            )
            VALUES
            (
                $uuid, $itemTag, $tier, $price, $displayName, $lore,
                $attributes, $extras, $start, $end, $lastSeen
            )
            ON CONFLICT(Uuid) DO UPDATE SET
                Price = excluded.Price,
                LastSeenAt = excluded.LastSeenAt;
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

    private int MarkExpiredMissing(HashSet<string> seenUuids)
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT Uuid FROM Auctions;";

        var toExpire = new List<string>();

        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                string uuid = reader.GetString(0);

                if (!seenUuids.Contains(uuid))
                    toExpire.Add(uuid);
            }
        }

        if (toExpire.Count == 0)
            return 0;

        using var transaction = connection.BeginTransaction();

        foreach (var uuid in toExpire)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Auctions WHERE Uuid = $uuid;";
            command.Parameters.AddWithValue("$uuid", uuid);
            command.ExecuteNonQuery();
        }

        transaction.Commit();

        return toExpire.Count;
    }
}

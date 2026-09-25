using HPSkyStatusUpdator.Configuration;
using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

// SaleHistory is kept forever and never trimmed. This job re-derives a
// small daily min/median/p90/count summary per item on top of it, so
// trend and prediction queries don't have to scan raw rows every time.
public class SaleHistoryAggregationService : BackgroundService
{
    private readonly MarketDatabaseService _market;
    private readonly SettingsService _settings;
    private readonly ServiceHealthService _health;
    private readonly ILogger<SaleHistoryAggregationService> _logger;

    public SaleHistoryAggregationService(
        MarketDatabaseService market,
        SettingsService settings,
        ServiceHealthService health,
        ILogger<SaleHistoryAggregationService> logger)
    {
        _market = market;
        _settings = settings;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _health.Beat("SaleHistoryAggregationService");

            try
            {
                Aggregate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sale history aggregation failed");
            }

            // NOTE: add AggregationIntervalHours to SettingKeys (default 6)
            int hours = _settings.GetInt(SettingKeys.AggregationIntervalHours, 6);
            hours = Math.Max(hours, 1);

            await Task.Delay(TimeSpan.FromHours(hours), stoppingToken);
        }
    }

    private void Aggregate()
    {
        using var connection = _market.GetConnection();
        connection.Open();

        // Re-derive the last 2 days every run (idempotent upsert), so
        // today's partial day stays current and yesterday gets finalized
        // once it's fully closed out. Older days are never touched again
        // once written.
        long since = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds();

        var groupCommand = connection.CreateCommand();
        groupCommand.CommandText =
        """
        SELECT
            ItemTag,
            date(SoldAt / 1000, 'unixepoch') AS Day,
            MIN(FinalPrice),
            COUNT(*)
        FROM SaleHistory
        WHERE SoldAt >= $since AND IsOutlier = 0
        GROUP BY ItemTag, Day;
        """;
        groupCommand.Parameters.AddWithValue("$since", since);

        var groups = new List<(string ItemTag, string Day, long Min, int Count)>();

        using (var reader = groupCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                groups.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetInt32(3)));
            }
        }

        using var transaction = connection.BeginTransaction();

        foreach (var group in groups)
        {
            var (median, p90) = GetPercentiles(connection, group.ItemTag, group.Day);

            var upsert = connection.CreateCommand();
            upsert.Transaction = transaction;
            upsert.CommandText =
            """
            INSERT INTO DailyPriceAggregates
            (ItemTag, Date, MinPrice, MedianPrice, P90Price, SaleCount)
            VALUES ($itemTag, $date, $min, $median, $p90, $count)
            ON CONFLICT(ItemTag, Date) DO UPDATE SET
                MinPrice = excluded.MinPrice,
                MedianPrice = excluded.MedianPrice,
                P90Price = excluded.P90Price,
                SaleCount = excluded.SaleCount;
            """;

            upsert.Parameters.AddWithValue("$itemTag", group.ItemTag);
            upsert.Parameters.AddWithValue("$date", group.Day);
            upsert.Parameters.AddWithValue("$min", group.Min);
            upsert.Parameters.AddWithValue("$median", median);
            upsert.Parameters.AddWithValue("$p90", p90);
            upsert.Parameters.AddWithValue("$count", group.Count);

            upsert.ExecuteNonQuery();
        }

        transaction.Commit();

        _logger.LogInformation(
            "Sale history aggregation: {Count} item/day buckets updated.",
            groups.Count);
    }

    // SQLite has no built-in percentile function, so this pulls the
    // sorted price list for one item/day bucket and indexes into it.
    // Only runs per (item, day) group, a few times a day — fine at this
    // scale.
    private static (long Median, long P90) GetPercentiles(
        SqliteConnection connection,
        string itemTag,
        string day)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT FinalPrice
        FROM SaleHistory
        WHERE ItemTag = $itemTag
          AND IsOutlier = 0
          AND date(SoldAt / 1000, 'unixepoch') = $day
        ORDER BY FinalPrice;
        """;
        command.Parameters.AddWithValue("$itemTag", itemTag);
        command.Parameters.AddWithValue("$day", day);

        var prices = new List<long>();

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                prices.Add(reader.GetInt64(0));
        }

        if (prices.Count == 0)
            return (0, 0);

        long median = prices[prices.Count / 2];
        long p90 = prices[(int)(prices.Count * 0.9)];

        return (median, p90);
    }
}

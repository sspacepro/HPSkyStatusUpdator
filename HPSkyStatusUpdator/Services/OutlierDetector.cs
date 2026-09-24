using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

// Sales are always stored — this only flags IsOutlier so downstream
// aggregate/prediction queries can filter them out, rather than throwing
// away data at ingest time.
public class OutlierDetector
{
    private const int SampleSize = 30;
    private const double OutlierMultiplier = 3.0;

    public bool IsOutlier(SqliteConnection connection, string itemTag, long price)
    {
        var recentPrices = GetRecentPrices(connection, itemTag);

        // Not enough history yet — don't flag anything during cold start.
        if (recentPrices.Count < 5)
            return false;

        recentPrices.Sort();

        double median = Median(recentPrices);

        if (median <= 0)
            return false;

        double ratio = price / median;

        return ratio > OutlierMultiplier || ratio < (1 / OutlierMultiplier);
    }

    private static List<long> GetRecentPrices(SqliteConnection connection, string itemTag)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT FinalPrice
        FROM SaleHistory
        WHERE ItemTag = $itemTag AND IsOutlier = 0
        ORDER BY SoldAt DESC
        LIMIT $limit;
        """;

        command.Parameters.AddWithValue("$itemTag", itemTag);
        command.Parameters.AddWithValue("$limit", SampleSize);

        using var reader = command.ExecuteReader();
        var prices = new List<long>();

        while (reader.Read())
            prices.Add(reader.GetInt64(0));

        return prices;
    }

    private static double Median(List<long> sorted)
    {
        int mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}

using Microsoft.Data.Sqlite;

namespace HPSkyStatusUpdator.Services;

// Sales are always stored — this only flags IsOutlier so downstream
// aggregate/prediction queries can filter them out, rather than
// throwing away data at ingest time.
//
// Compares "base price" (final price minus the estimated component
// value of applied upgrades) rather than raw price. A heavily
// enchanted/starred/gemmed item legitimately sells for a lot more than
// a bare copy of the same item — that's not an outlier, it's the
// upgrades. It's the *unexplained* portion of the price (what's left
// after subtracting what the upgrades were worth) that should be
// unusual for something to get flagged.
public class OutlierDetector
{
    private const int SampleSize = 30;
    private const double OutlierMultiplier = 3.0;

    public bool IsOutlier(
        SqliteConnection connection,
        string itemTag,
        long price,
        double? componentValue)
    {
        double basePrice = price - (componentValue ?? 0);

        var recentBasePrices = GetRecentBasePrices(connection, itemTag);

        // Not enough history yet — don't flag anything during cold start.
        if (recentBasePrices.Count < 5)
            return false;

        recentBasePrices.Sort();

        double median = Median(recentBasePrices);

        // A heavy component value estimate can push base price to zero
        // or negative for a legitimately expensive item — nothing
        // meaningful to compare a ratio against there, so don't flag it.
        if (median <= 0 || basePrice <= 0)
            return false;

        double ratio = basePrice / median;

        return ratio > OutlierMultiplier || ratio < (1 / OutlierMultiplier);
    }

    private static List<double> GetRecentBasePrices(SqliteConnection connection, string itemTag)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT FinalPrice, ComponentValue
        FROM SaleHistory
        WHERE ItemTag = $itemTag AND IsOutlier = 0
        ORDER BY SoldAt DESC
        LIMIT $limit;
        """;

        command.Parameters.AddWithValue("$itemTag", itemTag);
        command.Parameters.AddWithValue("$limit", SampleSize);

        using var reader = command.ExecuteReader();
        var basePrices = new List<double>();

        while (reader.Read())
        {
            long finalPrice = reader.GetInt64(0);
            double componentValue = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);

            basePrices.Add(finalPrice - componentValue);
        }

        return basePrices;
    }

    private static double Median(List<double> sorted)
    {
        int mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}

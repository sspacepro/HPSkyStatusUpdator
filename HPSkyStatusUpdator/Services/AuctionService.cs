using HPSkyStatusUpdator.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace HPSkyStatusUpdator.Services;

// Thin SQL query layer over market.db's Auctions table. No more
// in-memory cache/index to keep in sync — AuctionIngestService and
// AuctionFullSweepService keep that table current, and a SQLite index
// does the "find cheapest match" work instead of an in-memory LINQ scan.
public class AuctionService
{
    private readonly MarketDatabaseService _market;

    public AuctionService(MarketDatabaseService market)
    {
        _market = market;
    }

    // Applies the same "minimum bar, not exact spec" semantics the old
    // in-memory filter used for Recombobulated (null/false = don't care,
    // true = required) and extends it to Stars and pet XP (>=).
    public DecodedAuction? GetCheapestMatch(AuctionSearch search)
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT Uuid, ItemTag, Tier, Price, DisplayItemName, ItemLore,
               Attributes, Extras, StartTime, EndTime
        FROM Auctions
        WHERE ItemTag = $itemTag
          AND ($tier IS NULL OR Tier = $tier)
          AND (
                $stars IS NULL
                OR CAST(json_extract(Attributes, '$.Stars') AS INTEGER) >= $stars
              )
          AND (
                $recomb IS NULL
                OR $recomb = 0
                OR CAST(json_extract(Attributes, '$.Recombobulated') AS INTEGER) = 1
              )
          AND (
                $petXp IS NULL
                OR CAST(json_extract(Attributes, '$.PetExp') AS REAL) >= $petXp
              )
        ORDER BY Price ASC
        LIMIT 1;
        """;

        command.Parameters.AddWithValue("$itemTag", search.ItemTag);
        command.Parameters.AddWithValue("$tier", (object?)search.Tier ?? DBNull.Value);
        command.Parameters.AddWithValue("$stars", (object?)search.Stars ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$recomb",
            search.Recombobulated.HasValue
                ? (search.Recombobulated.Value ? 1 : 0)
                : (object)DBNull.Value);
        command.Parameters.AddWithValue("$petXp", (object?)search.PetXp ?? DBNull.Value);

        using var reader = command.ExecuteReader();

        return reader.Read() ? ReadAuction(reader) : null;
    }

    public IReadOnlyList<DecodedAuction> GetAllAuctions()
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT Uuid, ItemTag, Tier, Price, DisplayItemName, ItemLore,
               Attributes, Extras, StartTime, EndTime
        FROM Auctions;
        """;

        using var reader = command.ExecuteReader();
        var results = new List<DecodedAuction>();

        while (reader.Read())
            results.Add(ReadAuction(reader));

        return results;
    }

    public int GetAuctionCount()
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Auctions;";

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public DateTime GetLastRefreshTime()
    {
        using var connection = _market.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(LastSeenAt) FROM Auctions;";

        var result = command.ExecuteScalar();

        if (result == null || result is DBNull)
            return DateTime.MinValue;

        return DateTimeOffset
            .FromUnixTimeMilliseconds(Convert.ToInt64(result))
            .UtcDateTime;
    }

    private static DecodedAuction ReadAuction(SqliteDataReader reader)
    {
        return new DecodedAuction
        {
            Uuid = reader.GetString(0),
            ItemId = reader.GetString(1),
            Tier = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Price = reader.GetInt64(3),
            DisplayItemName = reader.GetString(4),
            ItemLore = reader.GetString(5),
            Attributes = JsonSerializer.Deserialize<ItemAttributes>(reader.GetString(6)) ?? new ItemAttributes(),
            Extras = JsonSerializer.Deserialize<ItemExtras>(reader.GetString(7)) ?? new ItemExtras(),
            Start = reader.GetInt64(8),
            End = reader.GetInt64(9)
        };
    }
}

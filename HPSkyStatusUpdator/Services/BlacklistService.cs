using HPSkyStatusUpdator.Models;

namespace HPSkyStatusUpdator.Services;

// Items here are never written to market.db at all — not the live
// Auctions table, not SaleHistory. This is for junk too cheap to be
// worth tracking, not a moderation tool.
public class BlacklistService
{
    private readonly DatabaseService _database;

    private volatile HashSet<string> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;

    public BlacklistService(DatabaseService database)
    {
        _database = database;
    }

    public bool IsBlacklisted(string itemTag)
    {
        EnsureLoaded();
        return _cache.Contains(itemTag);
    }

    public void Add(string itemTag, string? reason)
    {
        itemTag = itemTag.Trim().ToUpperInvariant();

        using var connection = _database.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO Blacklist (ItemTag, Reason, AddedAt)
        VALUES ($itemTag, $reason, $addedAt)
        ON CONFLICT(ItemTag) DO UPDATE SET Reason = excluded.Reason;
        """;

        command.Parameters.AddWithValue("$itemTag", itemTag);
        command.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$addedAt", DateTime.UtcNow);

        command.ExecuteNonQuery();

        Reload();
    }

    public bool Remove(string itemTag)
    {
        itemTag = itemTag.Trim().ToUpperInvariant();

        using var connection = _database.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Blacklist WHERE ItemTag = $itemTag;";
        command.Parameters.AddWithValue("$itemTag", itemTag);

        int rows = command.ExecuteNonQuery();

        Reload();

        return rows > 0;
    }

    public List<BlacklistedItem> GetAll()
    {
        using var connection = _database.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT ItemTag, Reason, AddedAt
        FROM Blacklist
        ORDER BY ItemTag;
        """;

        using var reader = command.ExecuteReader();
        var items = new List<BlacklistedItem>();

        while (reader.Read())
        {
            items.Add(new BlacklistedItem
            {
                ItemTag = reader.GetString(0),
                Reason = reader.IsDBNull(1) ? null : reader.GetString(1),
                AddedAt = reader.GetDateTime(2)
            });
        }

        return items;
    }

    private void EnsureLoaded()
    {
        if (!_loaded)
            Reload();
    }

    private void Reload()
    {
        using var connection = _database.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ItemTag FROM Blacklist;";

        using var reader = command.ExecuteReader();
        var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
            next.Add(reader.GetString(0));

        _cache = next;
        _loaded = true;
    }
}

namespace HPSkyStatusUpdator.Services;

public class RateLimitService
{
    private class RateData
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int Requests { get; set; }
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, RateData> _clients = new();

    public bool Check(string clientId, int maxRequests)
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_clients.TryGetValue(clientId, out var data))
            {
                data = new RateData();
                _clients[clientId] = data;
            }

            if ((now - data.WindowStart).TotalMinutes >= 1)
            {
                data.WindowStart = now;
                data.Requests = 0;
            }

            data.Requests++;

            return data.Requests <= maxRequests;
        }
    }
}

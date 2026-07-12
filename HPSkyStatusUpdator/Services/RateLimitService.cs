namespace HPSkyStatusUpdator.Services;

public class RateLimitService
{
    private class RateData
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int Requests { get; set; } = 0;
    }


    private readonly Dictionary<string, RateData> _clients = new();


    public bool Check(string clientId, int maxRequests)
    {
        var now = DateTime.UtcNow;


        if (!_clients.ContainsKey(clientId))
        {
            _clients[clientId] = new RateData();
        }


        var data = _clients[clientId];


        if ((now - data.WindowStart).TotalMinutes >= 1)
        {
            data.WindowStart = now;
            data.Requests = 0;
        }


        data.Requests++;


        return data.Requests <= maxRequests;
    }
}
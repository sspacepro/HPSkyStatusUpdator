namespace HPSkyStatusUpdator.Services;

public class ServiceHealthService
{
    private readonly Dictionary<string, DateTime> _heartbeats = new();

    public void Beat(string service)
    {
        lock (_heartbeats)
        {
            _heartbeats[service] = DateTime.UtcNow;
        }
    }


    public Dictionary<string, DateTime> GetStatus()
    {
        lock (_heartbeats)
        {
            return new Dictionary<string, DateTime>(_heartbeats);
        }
    }
}
namespace HPSkyStatusUpdator.Services;

public class ServiceHealthService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DateTime> _heartbeats = new();

    public void Beat(string service)
    {
        lock (_lock)
        {
            _heartbeats[service] = DateTime.UtcNow;
        }
    }

    public Dictionary<string, DateTime> GetStatus()
    {
        lock (_lock)
        {
            return new Dictionary<string, DateTime>(_heartbeats);
        }
    }

    public bool AreServicesHealthy(
        TimeSpan maxSilence,
        params string[] requiredServices)
    {
        lock (_lock)
        {
            if (requiredServices.Length == 0)
                return _heartbeats.Count > 0
                    && _heartbeats.Values.All(
                        beat => beat >= DateTime.UtcNow - maxSilence);

            var cutoff = DateTime.UtcNow - maxSilence;

            foreach (string service in requiredServices)
            {
                if (!_heartbeats.TryGetValue(service, out var beat)
                    || beat < cutoff)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

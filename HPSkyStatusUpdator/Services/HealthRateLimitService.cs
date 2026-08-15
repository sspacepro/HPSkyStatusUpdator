namespace HPSkyStatusUpdator.Services;

public class HealthRateLimitService
{
    private readonly object _lock = new();

    private readonly Dictionary<string, List<DateTime>> _attempts = new();

    private const int MaxRequests = 10;
    private static readonly TimeSpan Window =
        TimeSpan.FromSeconds(10);

    public bool Allow(string ip)
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_attempts.TryGetValue(ip, out var attempts))
            {
                attempts = new List<DateTime>();
                _attempts[ip] = attempts;
            }

            attempts.RemoveAll(
                x => now - x >= Window);

            if (attempts.Count >= MaxRequests)
            {
                return false;
            }

            attempts.Add(now);

            return true;
        }
    }
}
namespace HPSkyStatusUpdator.Services;

public class RegistrationService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<DateTime>> _attempts = new();

    public bool CanRegister(string ip)
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_attempts.TryGetValue(ip, out var attempts))
            {
                attempts = new List<DateTime>();
                _attempts[ip] = attempts;
            }

            // Remove attempts older than 1 hour
            attempts.RemoveAll(x => (now - x).TotalHours >= 1);

            // Max 5 registrations per hour
            if (attempts.Count >= 5)
            {
                return false;
            }

            attempts.Add(now);
            return true;
        }
    }
}

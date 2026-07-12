namespace HPSkyStatusUpdator.Services;

public class RegistrationService
{
    private readonly Dictionary<string, List<DateTime>> _attempts = new();


    public bool CanRegister(string ip)
    {
        var now = DateTime.UtcNow;


        if (!_attempts.ContainsKey(ip))
        {
            _attempts[ip] = new List<DateTime>();
        }


        // Remove attempts older than 1 hour
        _attempts[ip].RemoveAll(
            x => (now - x).TotalHours >= 1
        );


        // Max 5 registrations per hour
        if (_attempts[ip].Count >= 5)
        {
            return false;
        }


        _attempts[ip].Add(now);

        return true;
    }
}
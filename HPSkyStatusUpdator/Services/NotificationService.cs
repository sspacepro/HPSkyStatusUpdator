using System.Collections.Concurrent;

namespace HPSkyStatusUpdator.Services;

public class NotificationService
{
    private readonly ConcurrentDictionary<
        string,
        ConcurrentQueue<Models.Notification>
    > _queues = new();
}
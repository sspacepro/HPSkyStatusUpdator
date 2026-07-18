using System.Collections.Concurrent;

namespace HPSkyStatusUpdator.Services;

public class NotificationService
{
    private readonly ConcurrentDictionary<
        string,
        ConcurrentQueue<Models.Notification>
    > _queues = new();
    public void Add(string clientId, Models.Notification notification)
    {
        var queue = _queues.GetOrAdd(
            clientId,
            _ => new ConcurrentQueue<Models.Notification>()
        );

        queue.Enqueue(notification);
    }

    public List<Models.Notification> Get(string clientId)
    {
        List<Models.Notification> notifications = new();

        if (!_queues.TryGetValue(clientId, out var queue))
            return notifications;

        while (queue.TryDequeue(out var notification))
        {
            notifications.Add(notification);
        }

        return notifications;
    }
}

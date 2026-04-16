namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void DeferPublish(object notification);

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);
}

public static class NotificationPublisherExtensions
{
    extension(INotificationPublisher publisher)
    {
        public void DeferPublish<TNotification>(TNotification notification)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            publisher.DeferPublish(notification);
        }

        public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            return publisher.PublishAsync(notification, cancellationToken);
        }
    }
}

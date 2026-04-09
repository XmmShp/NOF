using NOF.Contract;

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
    void DeferPublish(INotification notification);

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}

using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Publishes a notification asynchronously.</summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deferred notification publisher for manually adding notifications to the transactional outbox context
/// without using HandlerBase.
/// </summary>
public interface IDeferredNotificationPublisher
{
    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void Publish(INotification notification);
}

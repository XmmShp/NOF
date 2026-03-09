using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Deferred notification publisher for adding notifications to the transactional outbox context.
/// Notifications will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
/// </summary>
public interface IDeferredNotificationPublisher
{
    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void Publish(INotification notification);
}

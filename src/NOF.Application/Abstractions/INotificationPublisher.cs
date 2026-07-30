using NOF.Contract;
namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when the active <see cref="IDbContext"/> saves changes.
    /// </summary>
    Task DeferPublishAsync(object notification, Type notificationType, Context context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an ordered notification to the transactional outbox context.
    /// The framework qualifies <paramref name="orderKey"/> with the producer service name and assigns its sequence transactionally.
    /// Every notification in one ordered stream must use the same key and reach each participating consumer handler without filtering gaps.
    /// Set <paramref name="completesOrderKey"/> only on the final notification in the stream.
    /// </summary>
    Task DeferPublishOrderedAsync(
        object notification,
        Type notificationType,
        string orderKey,
        Context context,
        bool completesOrderKey = false,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(object notification, Type notificationType, Context context, CancellationToken cancellationToken = default);
}

public static class NotificationPublisherExtensions
{
    extension(INotificationPublisher publisher)
    {
        public Task DeferPublishAsync<TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.DeferPublishAsync(notification, typeof(TNotification), context, cancellationToken);
        }

        public Task DeferPublishOrderedAsync<TNotification>(
            TNotification notification,
            string orderKey,
            Context context,
            bool completesOrderKey = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentException.ThrowIfNullOrWhiteSpace(orderKey);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.DeferPublishOrderedAsync(
                notification,
                typeof(TNotification),
                orderKey,
                context,
                completesOrderKey,
                cancellationToken);
        }

        public Task PublishAsync<TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(notification, typeof(TNotification), context, cancellationToken);
        }
    }
}

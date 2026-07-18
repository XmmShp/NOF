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

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(object notification, Type notificationType, Context context, CancellationToken cancellationToken = default);
}

public static class NotificationPublisherExtensions
{
    extension(INotificationPublisher publisher)
    {
        public Task PublishAsync(
            object notification,
            Type runtimeType,
            Context context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(runtimeType);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(notification, runtimeType, context, cancellationToken);
        }

        public Task DeferPublishAsync<TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.DeferPublishAsync(notification, typeof(TNotification), context, cancellationToken);
        }

        public Task DeferPublishAsync(
            object notification,
            Type runtimeType,
            Context context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(runtimeType);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.DeferPublishAsync(notification, runtimeType, context, cancellationToken);
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

using NOF.Abstraction;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when the active <see cref="Microsoft.EntityFrameworkCore.DbContext"/> saves changes.
    /// </summary>
    void DeferPublish(object notification, Type[] notificationTypes);

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default);
}

public static class NotificationPublisherExtensions
{
    extension(INotificationPublisher publisher)
    {
        public Task PublishAsync(
            object notification,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
            Context context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(runtimeType);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(notification, runtimeType.GetAllAssignableTypes(), context, cancellationToken);
        }

        public void DeferPublish<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TNotification>(TNotification notification)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            publisher.DeferPublish(notification, typeof(TNotification));
        }

        public void DeferPublish(
            object notification,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(runtimeType);
            publisher.DeferPublish(notification, runtimeType.GetAllAssignableTypes());
        }

        public Task PublishAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(context);
            return publisher.PublishAsync(notification, typeof(TNotification), context, cancellationToken);
        }
    }
}

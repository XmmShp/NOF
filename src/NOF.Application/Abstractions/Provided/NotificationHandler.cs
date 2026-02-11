using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for notification handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INotificationHandler : IMessageHandler;

/// <summary>
/// Handles notifications of the specified type.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for notification handlers, providing transactional message sending capabilities.
/// Works automatically via AsyncLocal without requiring any injected dependencies.
/// </summary>
public abstract class NotificationHandler<TNotification> : HandlerBase, INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    /// <inheritdoc />
    public abstract Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

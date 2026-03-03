using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for notification handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INotificationHandler : IMessageHandler
{
    Task HandleAsync(INotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Handles notifications of the specified type.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : class, INotification
{
    Task INotificationHandler.HandleAsync(INotification notification, CancellationToken cancellationToken)
        => HandleAsync((TNotification)notification, cancellationToken);

    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}


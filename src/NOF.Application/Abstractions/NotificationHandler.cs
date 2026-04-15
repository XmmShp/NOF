using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic base type for notification handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NotificationHandler : MessageHandler
{
    public abstract Task HandleAsync(INotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Handles notifications of the specified type.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public abstract class NotificationHandler<TNotification> : NotificationHandler
    where TNotification : class, INotification
{
    /// <inheritdoc />
    public sealed override Task HandleAsync(INotification notification, CancellationToken cancellationToken)
        => HandleAsync((TNotification)notification, cancellationToken);

    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

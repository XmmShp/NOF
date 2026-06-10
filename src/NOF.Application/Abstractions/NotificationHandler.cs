using NOF.Abstraction;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic base type for notification handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NotificationHandler
{
    public abstract Task HandleAsync(object notification, NOFContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Handles notifications of the specified type.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public abstract class NotificationHandler<TNotification> : NotificationHandler
{
    /// <inheritdoc />
    public sealed override Task HandleAsync(object notification, NOFContext context, CancellationToken cancellationToken)
        => HandleAsync((TNotification)notification, context, cancellationToken);

    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="context">Current NOF context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task HandleAsync(TNotification notification, NOFContext context, CancellationToken cancellationToken);
}

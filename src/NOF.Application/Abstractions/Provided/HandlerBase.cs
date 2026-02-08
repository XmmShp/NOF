using System.ComponentModel;

namespace NOF;

/// <summary>
/// Base class for handlers, providing transactional message sending capabilities.
/// Works automatically via AsyncLocal without requiring any injected dependencies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class HandlerBase
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="destinationEndpointName">Optional destination endpoint name.</param>
    protected void SendCommand(ICommand command, string? destinationEndpointName = null)
    {
        MessageOutboxContext.AddCommand(command, destinationEndpointName);
    }

    /// <summary>
    /// Adds a notification to the transactional outbox context.
    /// The notification will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    protected void PublishNotification(INotification notification)
    {
        MessageOutboxContext.AddNotification(notification);
    }
}

using System.ComponentModel;

namespace NOF.Abstraction;

/// <summary>
/// Non-generic marker interface for in-memory event handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventHandler
{
    Task HandleAsync(object @event, CancellationToken cancellationToken);
}

/// <summary>
/// Handles in-memory events of the specified type within the current scope.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventHandler<in TEvent> : IEventHandler
{
    Task IEventHandler.HandleAsync(object @event, CancellationToken cancellationToken)
        => HandleAsync((TEvent)@event, cancellationToken);

    /// <summary>Handles the event.</summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

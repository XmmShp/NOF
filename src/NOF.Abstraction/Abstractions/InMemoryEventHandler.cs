using System.ComponentModel;

namespace NOF.Abstraction;

/// <summary>
/// Non-generic base type for in-memory event handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class InMemoryEventHandler
{
    public abstract Task HandleAsync(object @event, CancellationToken cancellationToken);
}

/// <summary>
/// Handles in-memory events of the specified type within the current scope.
/// </summary>
/// <typeparam name="TEvent">The event payload type.</typeparam>
public abstract class InMemoryEventHandler<TEvent> : InMemoryEventHandler
{
    /// <inheritdoc />
    public sealed override Task HandleAsync(object @event, CancellationToken cancellationToken)
        => HandleAsync((TEvent)@event, cancellationToken);

    /// <summary>Handles the event.</summary>
    public abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

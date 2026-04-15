using System.ComponentModel;

namespace NOF.Abstraction;

/// <summary>
/// Non-generic invoker for event handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventHandlerInvoker
{
    Task HandleAsync(object payload, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for in-memory event handlers. Inherit from this to handle one payload type.
/// </summary>
/// <typeparam name="TPayload">The event payload type.</typeparam>
public abstract class EventHandler<TPayload> : ClassForSourceGenerator, IEventHandlerInvoker
{
    Task IEventHandlerInvoker.HandleAsync(object payload, CancellationToken cancellationToken)
        => HandleAsync((TPayload)payload, cancellationToken);

    public abstract Task HandleAsync(TPayload payload, CancellationToken cancellationToken);
}

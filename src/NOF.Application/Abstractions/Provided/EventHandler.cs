using NOF.Domain;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for event handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventHandler;

/// <summary>
/// Handles domain events of the specified type.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventHandler<in TEvent> : IEventHandler
    where TEvent : class, IEvent
{
    /// <summary>Handles the domain event.</summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}


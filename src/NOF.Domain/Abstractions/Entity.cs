namespace NOF;

/// <summary>
/// Marker interface for domain entities.
/// </summary>
public interface IEntity;

/// <summary>
/// Base class for domain entities.
/// </summary>
public abstract class Entity : IEntity
{
    /// <summary>Adds a domain event and invokes the specified action.</summary>
    /// <param name="event">The domain event.</param>
    /// <param name="action">The action to invoke with the event.</param>
    protected virtual void AddEvent(IEvent @event, Action<IEvent> action)
    {
        action.Invoke(@event);
    }
}

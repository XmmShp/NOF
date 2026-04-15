namespace NOF.Domain;

/// <summary>
/// Base class for aggregate root entities with domain event support.
/// </summary>
public abstract class AggregateRoot : IAggregateRoot
{
    /// <inheritdoc />
    public virtual ICollection<object> Events { get; } = [];

    /// <summary>Adds a domain event to the aggregate.</summary>
    /// <param name="event">The domain event to add.</param>
    protected virtual void AddEvent(object @event)
    {
        Events.Add(@event);
    }
}

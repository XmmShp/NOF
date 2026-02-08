namespace NOF;

/// <summary>
/// Represents an aggregate root entity that can raise domain events.
/// </summary>
public interface IAggregateRoot : IEntity
{
    /// <summary>Gets the collection of uncommitted domain events.</summary>
    IReadOnlyList<IEvent> Events { get; }
    /// <summary>Clears all uncommitted domain events.</summary>
    void ClearEvents();
}

/// <summary>
/// Base class for aggregate root entities with domain event support.
/// </summary>
public abstract class AggregateRoot : Entity, IAggregateRoot
{
    /// <summary>The mutable list of prepared domain events.</summary>
    protected List<IEvent> PreparedEvents = [];
    /// <inheritdoc />
    public virtual IReadOnlyList<IEvent> Events => PreparedEvents.AsReadOnly();

    /// <summary>Adds a domain event to the aggregate.</summary>
    /// <param name="event">The domain event to add.</param>
    protected virtual void AddEvent(IEvent @event)
    {
        PreparedEvents.Add(@event);
    }

    /// <inheritdoc />
    public virtual void ClearEvents() => PreparedEvents.Clear();
}

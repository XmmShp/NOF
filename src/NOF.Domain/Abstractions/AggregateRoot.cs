namespace NOF;

public interface IAggregateRoot : IEntity
{
    IReadOnlyList<IEvent> Events { get; }
    void ClearEvents();
}

public interface IAggregateRoot<TKey> : IAggregateRoot
    where TKey : struct;

public abstract class AggregateRoot<TKey> : Entity, IAggregateRoot<TKey>
    where TKey : struct
{
    public TKey Id { get; init; }
    protected List<IEvent> PreparedEvents = [];
    public virtual IReadOnlyList<IEvent> Events => PreparedEvents.AsReadOnly();

    protected virtual void AddEvent(IEvent @event)
    {
        PreparedEvents.Add(@event);
    }

    public virtual void ClearEvents() => PreparedEvents.Clear();
}

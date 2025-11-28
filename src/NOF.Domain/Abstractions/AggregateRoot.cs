namespace NOF;

public interface IAggregateRoot : IEntity
{
    IReadOnlyList<IEvent> Events { get; }
    void ClearEvents();
}

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    protected List<IEvent> PreparedEvents = [];
    public virtual IReadOnlyList<IEvent> Events => PreparedEvents.AsReadOnly();

    protected virtual void AddEvent(IEvent @event)
    {
        PreparedEvents.Add(@event);
    }

    public virtual void ClearEvents() => PreparedEvents.Clear();
}

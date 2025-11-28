namespace NOF;

public interface IEntity;

public abstract class Entity : IEntity
{
    protected virtual void AddEvent(IEvent @event, Action<IEvent> action)
    {
        action.Invoke(@event);
    }
}

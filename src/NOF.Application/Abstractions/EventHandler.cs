namespace NOF;

public interface IEventHandler;

public interface IEventHandler<TEvent> : IEventHandler
    where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
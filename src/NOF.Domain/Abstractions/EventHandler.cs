using MassTransit;

namespace NOF;

[ExcludeFromTopology]
public interface IEventHandler;

[ExcludeFromTopology]
public abstract class EventHandler<TEvent> : IConsumer<TEvent>, IEventHandler
    where TEvent : class, IEvent
{
    public Task Consume(ConsumeContext<TEvent> context)
    {
        return HandleAsync(context.Message, context.CancellationToken);
    }

    public abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

using MassTransit.Mediator;

namespace NOF;

public class EventPublisher : IEventPublisher
{
    private readonly IScopedMediator _mediator;

    public EventPublisher(IScopedMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Publish(@event as object, cancellationToken);
    }
}

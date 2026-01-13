using MassTransit.Mediator;

namespace NOF;

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IScopedMediator _mediator;

    public MassTransitEventPublisher(IScopedMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Publish(@event, cancellationToken);
    }
}

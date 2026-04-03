using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Domain;

namespace NOF.Infrastructure;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}

public sealed class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public EventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        var infos = _serviceProvider.GetRequiredService<HandlerInfos>();
        var eventType = @event.GetType();
        var handlerTypes = infos.GetEventHandlers(eventType);
        foreach (var handlerType in handlerTypes)
        {
            var handler = (IEventHandler)_serviceProvider.GetRequiredService(handlerType);
            await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }
}

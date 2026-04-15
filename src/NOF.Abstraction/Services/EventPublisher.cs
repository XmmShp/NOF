using Microsoft.Extensions.DependencyInjection;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public EventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync(object payload, Type runtimeType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(runtimeType);

        var infos = _serviceProvider.GetRequiredService<EventHandlerInfos>();
        foreach (var handlerType in infos.GetHandlerTypes(runtimeType))
        {
            var handler = (IEventHandler)_serviceProvider.GetRequiredService(handlerType);
            await handler.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }
}

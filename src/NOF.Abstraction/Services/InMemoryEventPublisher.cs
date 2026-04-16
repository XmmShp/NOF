namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventHandlerInfos _eventHandlerInfos;

    public InMemoryEventPublisher(IServiceProvider serviceProvider, EventHandlerInfos eventHandlerInfos)
    {
        _serviceProvider = serviceProvider;
        _eventHandlerInfos = eventHandlerInfos;
    }

    public async Task PublishAsync(object payload, Type[] eventTypes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);

        foreach (var eventType in eventTypes)
        {
            foreach (var handlerType in _eventHandlerInfos.GetHandlerTypes(eventType))
            {
                if (_serviceProvider.GetService(handlerType) is not InMemoryEventHandler handler)
                {
                    throw new InvalidOperationException($"Event handler type '{handlerType}' is not registered in the current scope.");
                }

                await handler.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

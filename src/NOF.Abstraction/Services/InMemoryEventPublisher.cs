using NOF.Contract;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventHandlerRegistry _eventHandlerRegistry;

    public InMemoryEventPublisher(IServiceProvider serviceProvider, EventHandlerRegistry eventHandlerRegistry)
    {
        _serviceProvider = serviceProvider;
        _eventHandlerRegistry = eventHandlerRegistry;
    }

    public async Task PublishAsync(
        object payload,
        Type[] eventTypes,
        Context context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);
        ArgumentNullException.ThrowIfNull(context);

        using var publisherScope = EventPublisher.PushPublisherIfNeeded(this);
        using var contextScope = EventPublisher.PushContext(context);
        foreach (var eventType in eventTypes)
        {
            foreach (var handlerType in _eventHandlerRegistry.GetHandlerTypes(eventType))
            {
                if (_serviceProvider.GetService(handlerType) is not InMemoryEventHandler handler)
                {
                    throw new InvalidOperationException($"Event handler type '{handlerType}' is not registered in the current scope.");
                }

                await handler.HandleAsync(payload, context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

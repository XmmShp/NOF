using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object payload, Type[] eventTypes, CancellationToken cancellationToken);
}

public static class EventPublisherExtensions
{
    extension(IEventPublisher publisher)
    {
        public Task PublishAsync(
            object payload,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(runtimeType);
            return publisher.PublishAsync(payload, DispatchTypeUtilities.GetSelfAndBaseTypesAndInterfaces(runtimeType), cancellationToken);
        }

        public Task PublishAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(
        TPayload payload,
        CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publisher);
            ArgumentNullException.ThrowIfNull(payload);
            return publisher.PublishAsync(payload, typeof(TPayload), cancellationToken);
        }
    }
}

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventHandlerInfos _eventHandlerInfos;

    public EventPublisher(IServiceProvider serviceProvider, EventHandlerInfos eventHandlerInfos)
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
                var handler = _serviceProvider.GetService(handlerType) as InMemoryEventHandler;
                if (handler is null)
                {
                    throw new InvalidOperationException($"Event handler type '{handlerType}' is not registered in the current scope.");
                }

                await handler.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

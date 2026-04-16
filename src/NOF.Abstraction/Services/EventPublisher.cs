using System.Diagnostics.CodeAnalysis;
using System.Threading;

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
    private static readonly AsyncLocal<IEventPublisher?> _currentPublisher = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly EventHandlerInfos _eventHandlerInfos;

    public EventPublisher(IServiceProvider serviceProvider, EventHandlerInfos eventHandlerInfos)
    {
        _serviceProvider = serviceProvider;
        _eventHandlerInfos = eventHandlerInfos;
    }

    public static IDisposable PushCurrent(IEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        var previous = _currentPublisher.Value;
        _currentPublisher.Value = publisher;
        return new AmbientPublisherScope(previous);
    }

    public static void PublishEvent(object payload, Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);

        var publisher = _currentPublisher.Value
            ?? throw new InvalidOperationException("No ambient IEventPublisher is available for the current async flow.");
        publisher.PublishAsync(payload, eventTypes, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void PublishEvent(
        object payload,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        PublishEvent(payload, DispatchTypeUtilities.GetSelfAndBaseTypesAndInterfaces(runtimeType));
    }

    public static void PublishEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        PublishEvent(payload, typeof(TPayload));
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

    private sealed class AmbientPublisherScope : IDisposable
    {
        private readonly IEventPublisher? _previousPublisher;
        private bool _disposed;

        public AmbientPublisherScope(IEventPublisher? previousPublisher)
        {
            _previousPublisher = previousPublisher;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentPublisher.Value = _previousPublisher;
            _disposed = true;
        }
    }
}

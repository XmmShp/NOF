using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Publishes in-memory events to handlers resolved from the current scope.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object payload, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType, CancellationToken cancellationToken);
}

public static class EventPublisherExtensions
{
    extension(IEventPublisher publisher)
    {
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

    public async Task PublishAsync(object payload, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(runtimeType);

        foreach (var handlerType in _eventHandlerInfos.GetHandlerTypes(runtimeType))
        {
            var handler = (IEventHandlerInvoker)_serviceProvider.GetRequiredService(handlerType);
            await handler.HandleAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }
}

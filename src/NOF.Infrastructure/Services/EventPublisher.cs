using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Domain;

namespace NOF.Infrastructure;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory event publisher that dispatches domain events to their typed handlers
/// resolved from the root DI container using a composite keyed service key
/// <c>(<see cref="EventHandlerKey"/>, eventType)</c>.
/// Fully AOT-compatible supported.No reflection or <c>MakeGenericType</c> calls.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public EventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _serviceProvider.GetKeyedServices<IEventHandler>(EventHandlerKey.Of(@event.GetType())))
        {
            await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }
}

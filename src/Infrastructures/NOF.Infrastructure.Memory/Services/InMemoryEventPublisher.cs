using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Memory;

/// <summary>
/// In-memory event publisher that dispatches domain events to their typed handlers
/// resolved from the root DI container using a composite keyed service key
/// <c>(<see cref="EventHandlerKey"/>, eventType)</c>.
/// Fully AOT-compatible — no reflection or <c>MakeGenericType</c> calls.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public InMemoryEventPublisher(IServiceProvider serviceProvider)
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

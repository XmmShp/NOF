using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Memory;

/// <summary>
/// In-memory notification rider that dispatches notifications to all typed handlers
/// resolved from DI using keyed services (multicast).
/// Creates a new DI scope per dispatch to match MassTransit behavior.
/// Fully AOT-compatible no reflection or <c>MakeGenericType</c> calls.
/// </summary>
public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MemoryNotificationRider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task PublishAsync(INotification notification,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var notificationType = notification.GetType();
        var handlers = scope.ServiceProvider.GetKeyedServices<INotificationHandler>(NotificationHandlerKey.Of(notificationType));
        var pipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();

        foreach (var handler in handlers)
        {
            var context = new InboundContext
            {
                Message = notification,
                Handler = handler,
                Headers = headers is not null
                    ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            };

            await pipeline.ExecuteAsync(context,
                ct => new ValueTask(handler.HandleAsync(notification, ct)),
                cancellationToken).ConfigureAwait(false);
        }
    }
}

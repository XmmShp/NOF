using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly IServiceProvider _rootServiceProvider;

    public MemoryNotificationRider(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider;
    }

    public async Task PublishAsync(INotification notification,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var infos = scope.ServiceProvider.GetRequiredService<HandlerInfos>();
        var handlerTypes = infos.GetNotificationHandlers(notification.GetType());
        foreach (var handlerType in handlerTypes)
        {
            await InboundHandlerInvoker.ExecuteNotificationToHandlerAsync(
                _rootServiceProvider,
                notification,
                handlerType,
                headers,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

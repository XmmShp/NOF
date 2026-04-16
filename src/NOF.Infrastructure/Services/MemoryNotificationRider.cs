using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly IServiceProvider _rootServiceProvider;

    public MemoryNotificationRider(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider;
    }

    public async Task PublishAsync(object notification,
        Type[] notificationTypes,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        await using var scope = _rootServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var infos = scope.ServiceProvider.GetRequiredService<NotificationHandlerInfos>();
        var seenHandlerTypes = new HashSet<Type>();
        foreach (var notificationType in notificationTypes)
        {
            foreach (var handlerType in infos.GetHandlers(notificationType))
            {
                if (!seenHandlerTypes.Add(handlerType))
                {
                    continue;
                }

                await InboundHandlerInvoker.ExecuteNotificationToHandlerAsync(
                    _rootServiceProvider,
                    notification,
                    handlerType,
                    headers,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

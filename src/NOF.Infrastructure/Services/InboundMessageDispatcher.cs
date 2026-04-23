using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class InboundMessageDispatcher
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly IObjectSerializer _serializer;
    private readonly CommandHandlerInfos? _commandHandlerInfos;
    private readonly NotificationHandlerInfos? _notificationHandlerInfos;

    public InboundMessageDispatcher(
        IServiceProvider rootServiceProvider,
        IObjectSerializer serializer,
        CommandHandlerInfos? commandHandlerInfos,
        NotificationHandlerInfos? notificationHandlerInfos)
    {
        _rootServiceProvider = rootServiceProvider;
        _serializer = serializer;
        _commandHandlerInfos = commandHandlerInfos;
        _notificationHandlerInfos = notificationHandlerInfos;
    }

    public async Task DispatchCommandAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var commandType = TypeRegistry.Resolve(commandTypeName);
        var command = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize command payload as '{payloadTypeName}'.");

        var handlerType = _commandHandlerInfos?.GetHandlers(commandType).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Cannot route command '{commandType.Name}'. No matching handler registered.");

        await DispatchCommandToHandlerAsync(
            payload,
            payloadTypeName,
            handlerType,
            headers,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DispatchCommandToHandlerAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var command = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize command payload as '{payloadTypeName}'.");

        var context = new CommandInboundContext
        {
            Message = command,
            HandlerType = handlerType
        };

        var pipeline = _rootServiceProvider.GetRequiredService<CommandInboundPipelineExecutor>();
        await pipeline.ExecuteAsync(
            context,
            headers,
            sp => async ct =>
            {
                var handler = (CommandHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(command, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DispatchNotificationAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> notificationTypeNames,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var notification = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize notification payload as '{payloadTypeName}'.");

        await using var scope = _rootServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var infos = _notificationHandlerInfos ?? scope.ServiceProvider.GetRequiredService<NotificationHandlerInfos>();
        var seenHandlerTypes = new HashSet<Type>();

        foreach (var notificationTypeName in notificationTypeNames)
        {
            var notificationType = TypeRegistry.Resolve(notificationTypeName);
            foreach (var handlerType in infos.GetHandlers(notificationType))
            {
                if (!seenHandlerTypes.Add(handlerType))
                {
                    continue;
                }

                await ExecuteNotificationToHandlerAsync(
                    notification,
                    handlerType,
                    headers,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task DispatchNotificationToHandlerAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var notification = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize notification payload as '{payloadTypeName}'.");

        await ExecuteNotificationToHandlerAsync(
            notification,
            handlerType,
            headers,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteNotificationToHandlerAsync(
        object notification,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        var context = new NotificationInboundContext
        {
            Message = notification,
            HandlerType = handlerType
        };

        var pipeline = _rootServiceProvider.GetRequiredService<NotificationInboundPipelineExecutor>();
        await pipeline.ExecuteAsync(
            context,
            headers,
            sp => async ct =>
            {
                var handler = (NotificationHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(notification, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }
}

using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MessageTypeResolver
{
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;

    public MessageTypeResolver(
        CommandHandlerRegistry commandHandlerRegistry,
        NotificationHandlerRegistry notificationHandlerRegistry)
    {
        _commandHandlerRegistry = commandHandlerRegistry;
        _notificationHandlerRegistry = notificationHandlerRegistry;
    }

    public Type Resolve(string messageTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

        if (_commandHandlerRegistry.TryGetCommandType(messageTypeName, out var commandType))
        {
            return commandType;
        }

        if (_notificationHandlerRegistry.TryGetNotificationType(messageTypeName, out var notificationType))
        {
            return notificationType;
        }

        throw new InvalidOperationException($"Message type '{messageTypeName}' is not registered in the generated handler registries.");
    }

    public Type[] Resolve(IEnumerable<string> messageTypeNames)
    {
        ArgumentNullException.ThrowIfNull(messageTypeNames);
        return [.. messageTypeNames.Select(Resolve)];
    }
}

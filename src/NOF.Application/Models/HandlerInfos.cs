namespace NOF.Application;

/// <summary>
/// Centralized registry of all handler metadata collected during service registration.
/// Contains typed sets for each handler kind.
/// Registered as a singleton in DI.
/// </summary>
public sealed class HandlerInfos
{
    public HashSet<CommandHandlerInfo> Commands { get; } = [];
    public HashSet<EventHandlerInfo> Events { get; } = [];
    public HashSet<NotificationHandlerInfo> Notifications { get; } = [];

    private readonly Dictionary<Type, List<Type>> _commandByMessage = new();
    private readonly Dictionary<Type, List<Type>> _eventByMessage = new();
    private readonly Dictionary<Type, List<Type>> _notificationByMessage = new();

    /// <summary>
    /// Adds a handler info to the appropriate typed set using pattern matching.
    /// </summary>
    public void Add(HandlerInfo info)
    {
        switch (info)
        {
            case CommandHandlerInfo command:
                Commands.Add(command);
                if (!_commandByMessage.TryGetValue(command.CommandType, out var commandHandlers))
                {
                    commandHandlers = [];
                    _commandByMessage[command.CommandType] = commandHandlers;
                }
                if (!commandHandlers.Contains(command.HandlerType))
                {
                    commandHandlers.Add(command.HandlerType);
                }
                break;
            case EventHandlerInfo @event:
                Events.Add(@event);
                if (!_eventByMessage.TryGetValue(@event.EventType, out var eventHandlers))
                {
                    eventHandlers = [];
                    _eventByMessage[@event.EventType] = eventHandlers;
                }
                if (!eventHandlers.Contains(@event.HandlerType))
                {
                    eventHandlers.Add(@event.HandlerType);
                }
                break;
            case NotificationHandlerInfo notification:
                Notifications.Add(notification);
                if (!_notificationByMessage.TryGetValue(notification.NotificationType, out var notificationHandlers))
                {
                    notificationHandlers = [];
                    _notificationByMessage[notification.NotificationType] = notificationHandlers;
                }
                if (!notificationHandlers.Contains(notification.HandlerType))
                {
                    notificationHandlers.Add(notification.HandlerType);
                }
                break;
            default:
                throw new ArgumentException($"Unknown handler info type: {info.GetType()}", nameof(info));
        }
    }

    /// <summary>
    /// Adds multiple handler infos to the appropriate typed sets.
    /// </summary>
    public void AddRange(ReadOnlySpan<HandlerInfo> infos)
    {
        foreach (var info in infos)
        {
            Add(info);
        }
    }

    public IReadOnlyList<Type> GetCommandHandlers(Type commandType)
        => _commandByMessage.TryGetValue(commandType, out var list) ? list : Array.Empty<Type>();

    public IReadOnlyList<Type> GetEventHandlers(Type eventType)
        => _eventByMessage.TryGetValue(eventType, out var list) ? list : Array.Empty<Type>();

    public IReadOnlyList<Type> GetNotificationHandlers(Type notificationType)
        => _notificationByMessage.TryGetValue(notificationType, out var list) ? list : Array.Empty<Type>();
}

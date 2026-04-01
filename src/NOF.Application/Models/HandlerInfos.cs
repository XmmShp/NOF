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

    /// <summary>
    /// Adds a handler info to the appropriate typed set using pattern matching.
    /// </summary>
    public void Add(HandlerInfo info)
    {
        switch (info)
        {
            case CommandHandlerInfo command:
                Commands.Add(command);
                break;
            case EventHandlerInfo @event:
                Events.Add(@event);
                break;
            case NotificationHandlerInfo notification:
                Notifications.Add(notification);
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
}

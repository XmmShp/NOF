namespace NOF.Infrastructure;

/// <summary>
/// Strongly-typed marker used as keyed-service key for command handler registrations.
/// The key is <c>CommandHandlerKey.Of(typeof(TCommand))</c>.
/// </summary>
public sealed record CommandHandlerKey(CommandHandlerKey.UniqueKey Key, Type CommandType)
{
    public class UniqueKey
    {
        private UniqueKey() { }
        public static readonly UniqueKey Instance = new();
    }

    /// <summary>Creates the composite service key for the given command type.</summary>
    public static CommandHandlerKey Of(Type commandType) => new(UniqueKey.Instance, commandType);
}

/// <summary>
/// Strongly-typed marker used as keyed-service key for event handler registrations.
/// The key is <c>EventHandlerKey.Of(typeof(TEvent))</c>.
/// </summary>
public sealed record EventHandlerKey(EventHandlerKey.UniqueKey Key, Type EventType)
{
    public class UniqueKey
    {
        private UniqueKey() { }
        public static readonly UniqueKey Instance = new();
    }

    /// <summary>Creates the composite service key for the given event type.</summary>
    public static EventHandlerKey Of(Type eventType) => new(UniqueKey.Instance, eventType);
}

/// <summary>
/// Strongly-typed marker used as keyed-service key for notification handler registrations.
/// The key is <c>NotificationHandlerKey.Of(typeof(TNotification))</c>.
/// </summary>
public sealed record NotificationHandlerKey(NotificationHandlerKey.UniqueKey Key, Type NotificationType)
{
    public class UniqueKey
    {
        private UniqueKey() { }
        public static readonly UniqueKey Instance = new();
    }

    /// <summary>Creates the composite service key for the given notification type.</summary>
    public static NotificationHandlerKey Of(Type notificationType) => new(UniqueKey.Instance, notificationType);
}

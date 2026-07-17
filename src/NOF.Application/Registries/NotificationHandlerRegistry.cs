using NOF.Abstraction;

namespace NOF.Application;

public sealed class NotificationHandlerRegistry : Registry<NotificationHandlerRegistration>
{
    private readonly Dictionary<Type, List<Type>> _handlersByNotification = [];
    private readonly Dictionary<string, List<Type>> _handlersByNotificationName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _notificationTypesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _handlerTypesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _invokerTypesByHandlerName = new(StringComparer.Ordinal);

    public NotificationHandlerRegistry()
    {
        Frozen += BuildIndexes;
    }

    public IReadOnlyCollection<Type> GetHandlers(Type notificationType)
    {
        ArgumentNullException.ThrowIfNull(notificationType);
        Freeze();
        return _handlersByNotification.TryGetValue(notificationType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    public IReadOnlyCollection<Type> GetHandlers(string notificationTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationTypeName);
        Freeze();
        return _handlersByNotificationName.TryGetValue(notificationTypeName, out var handlers) ? handlers : Array.Empty<Type>();
    }

    public bool TryGetNotificationType(string notificationTypeName, out Type notificationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationTypeName);
        Freeze();
        return _notificationTypesByName.TryGetValue(notificationTypeName, out notificationType!);
    }

    public bool TryGetHandlerType(string handlerTypeName, out Type handlerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);
        Freeze();
        return _handlerTypesByName.TryGetValue(handlerTypeName, out handlerType!);
    }

    public bool TryGetInvokerType(string handlerTypeName, out Type invokerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);
        Freeze();
        return _invokerTypesByHandlerName.TryGetValue(handlerTypeName, out invokerType!);
    }

    private void BuildIndexes()
    {
        _handlersByNotification.Clear();
        _handlersByNotificationName.Clear();
        _notificationTypesByName.Clear();
        _handlerTypesByName.Clear();
        _invokerTypesByHandlerName.Clear();

        foreach (var registration in Items)
        {
            Index(registration);
        }
    }

    private void Index(NotificationHandlerRegistration registration)
    {
        if (!_handlersByNotification.TryGetValue(registration.NotificationType, out var handlers))
        {
            handlers = [];
            _handlersByNotification[registration.NotificationType] = handlers;
        }

        if (!handlers.Contains(registration.HandlerType))
        {
            handlers.Add(registration.HandlerType);
        }

        if (!_handlersByNotificationName.TryGetValue(registration.NotificationTypeName, out var handlersByName))
        {
            handlersByName = [];
            _handlersByNotificationName[registration.NotificationTypeName] = handlersByName;
        }

        if (!handlersByName.Contains(registration.HandlerType))
        {
            handlersByName.Add(registration.HandlerType);
        }

        _notificationTypesByName.TryAdd(registration.NotificationTypeName, registration.NotificationType);
        _handlerTypesByName.TryAdd(registration.HandlerTypeName, registration.HandlerType);
        _invokerTypesByHandlerName.TryAdd(registration.HandlerTypeName, registration.InvokerType);
    }
}

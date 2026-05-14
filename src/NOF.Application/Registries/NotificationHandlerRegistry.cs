using NOF.Abstraction;

namespace NOF.Application;

public sealed class NotificationHandlerRegistry : Registry<NotificationHandlerRegistration>
{
    private readonly Dictionary<Type, List<Type>> _handlersByNotification = [];

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

    private void BuildIndexes()
    {
        _handlersByNotification.Clear();
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
    }
}

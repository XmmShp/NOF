using NOF.Abstraction;

namespace NOF.Application;

public sealed class NotificationHandlerInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<NotificationHandlerRegistration> _registrations = [];
    private readonly Dictionary<Type, List<Type>> _handlersByNotification = new();
    private bool _isInitialized;

    public NotificationHandlerInfos()
        : this(new Registry())
    {
    }

    public NotificationHandlerInfos(Registry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<NotificationHandlerRegistration> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
    }

    public void Add(NotificationHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        AddCore(registration);
    }

    public void AddRange(ReadOnlySpan<NotificationHandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            AddCore(registration);
        }
    }

    public IReadOnlyCollection<Type> GetHandlers(Type notificationType)
    {
        ArgumentNullException.ThrowIfNull(notificationType);
        EnsureInitialized();
        return _handlersByNotification.TryGetValue(notificationType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_initializeGate)
        {
            if (_isInitialized)
            {
                return;
            }

            foreach (var registration in _registry.NotificationHandlerRegistrations)
            {
                AddCore(registration);
            }

            _registrations.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(NotificationHandlerRegistration registration)
    {
        _registrations.Add(registration);
        Index(registration);
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

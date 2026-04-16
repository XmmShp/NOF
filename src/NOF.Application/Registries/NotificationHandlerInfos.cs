using NOF.Abstraction;

namespace NOF.Application;

public sealed class NotificationHandlerInfos
{
    private readonly Lock _gate = new();
    private readonly HashSet<NotificationHandlerRegistration> _registrations = [];
    private readonly Dictionary<Type, List<Type>> _handlersByNotification = new();
    private bool _isFrozen;

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

        lock (_gate)
        {
            ThrowIfFrozen();
            AddCore(registration);
        }
    }

    public void AddRange(ReadOnlySpan<NotificationHandlerRegistration> registrations)
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            foreach (var registration in registrations)
            {
                AddCore(registration);
            }
        }
    }

    public void Freeze()
    {
        EnsureInitialized();
    }

    public IReadOnlyCollection<Type> GetHandlers(Type notificationType)
    {
        ArgumentNullException.ThrowIfNull(notificationType);
        EnsureInitialized();
        return _handlersByNotification.TryGetValue(notificationType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    private void EnsureInitialized()
    {
        if (_isFrozen)
        {
            return;
        }

        lock (_gate)
        {
            if (_isFrozen)
            {
                return;
            }

            foreach (var registration in Registry.NotificationHandlerRegistrations)
            {
                if (registration is NotificationHandlerRegistration typedRegistration)
                {
                    AddCore(typedRegistration);
                }
            }

            _isFrozen = true;
        }
    }

    private void AddCore(NotificationHandlerRegistration registration)
    {
        _registrations.Add(registration);
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

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("NotificationHandlerInfos is frozen after its first read.");
        }
    }
}

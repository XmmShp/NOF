namespace NOF.Abstraction;

/// <summary>
/// Centralized registry of in-memory event handler metadata collected during service registration.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerInfos
{
    private readonly Lock _gate = new();
    private readonly HashSet<EventHandlerRegistration> _events = [];
    private readonly Dictionary<Type, List<Type>> _eventByMessage = new();
    private bool _isFrozen;

    public IReadOnlyCollection<EventHandlerRegistration> Events
    {
        get
        {
            EnsureInitialized();
            return _events;
        }
    }

    public void Add(EventHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            ThrowIfFrozen();
            AddCore(registration);
        }
    }

    public void AddRange(ReadOnlySpan<EventHandlerRegistration> registrations)
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

    public IReadOnlyList<Type> GetHandlerTypes(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        EnsureInitialized();
        return _eventByMessage.TryGetValue(eventType, out var handlers) ? handlers : Array.Empty<Type>();
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

            foreach (var registration in Registry.EventHandlerRegistrations)
            {
                AddCore(registration);
            }

            _isFrozen = true;
        }
    }

    private void AddCore(EventHandlerRegistration registration)
    {
        _events.Add(registration);
        if (!_eventByMessage.TryGetValue(registration.EventType, out var eventHandlers))
        {
            eventHandlers = [];
            _eventByMessage[registration.EventType] = eventHandlers;
        }

        if (!eventHandlers.Contains(registration.HandlerType))
        {
            eventHandlers.Add(registration.HandlerType);
        }
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("EventHandlerInfos is frozen after its first read.");
        }
    }
}

namespace NOF.Abstraction;

/// <summary>
/// Centralized registry of in-memory event handler metadata collected during service registration.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<EventHandlerRegistration> _events = [];
    private readonly Dictionary<Type, List<Type>> _eventByMessage = new();
    private bool _isInitialized;

    public EventHandlerInfos()
        : this(new Registry())
    {
    }

    public EventHandlerInfos(Registry registry)
    {
        _registry = registry;
    }

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
        AddCore(registration);
    }

    public void AddRange(ReadOnlySpan<EventHandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            AddCore(registration);
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

            foreach (var registration in _registry.EventHandlerRegistrations)
            {
                AddCore(registration);
            }

            _events.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(EventHandlerRegistration registration)
    {
        _events.Add(registration);
        Index(registration);
    }

    private void Index(EventHandlerRegistration registration)
    {
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
}

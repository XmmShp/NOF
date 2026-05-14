namespace NOF.Abstraction;

/// <summary>
/// Centralized registry of in-memory event handler metadata collected during service registration.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerRegistry : Registry<EventHandlerRegistration>
{
    private readonly Dictionary<Type, List<Type>> _eventByMessage = [];

    public EventHandlerRegistry()
    {
        Frozen += BuildIndexes;
    }

    public IReadOnlyList<Type> GetHandlerTypes(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        Freeze();
        return _eventByMessage.TryGetValue(eventType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    private void BuildIndexes()
    {
        _eventByMessage.Clear();
        foreach (var registration in Items)
        {
            Index(registration);
        }
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

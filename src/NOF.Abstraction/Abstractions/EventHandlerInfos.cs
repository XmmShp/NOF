using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Centralized registry of in-memory event handler metadata collected during service registration.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerInfos
{
    private readonly object _gate = new();
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

    public IReadOnlyList<Type> GetHandlerTypes(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        EnsureInitialized();

        var result = new List<Type>();
        var seenHandlerTypes = new HashSet<Type>();
        foreach (var dispatchType in EnumerateDispatchTypes(runtimeType))
        {
            if (!_eventByMessage.TryGetValue(dispatchType, out var handlers))
            {
                continue;
            }

            foreach (var handlerType in handlers)
            {
                if (seenHandlerTypes.Add(handlerType))
                {
                    result.Add(handlerType);
                }
            }
        }

        return result;
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

            foreach (var registration in EventHandlerRegistry.GetRegistrations())
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

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Runtime event dispatch inspects base types and interfaces by design.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Runtime event dispatch inspects base types and interfaces by design.")]
    private static IEnumerable<Type> EnumerateDispatchTypes(Type runtimeType)
    {
        if (runtimeType == typeof(object))
        {
            yield return typeof(object);
            yield break;
        }

        for (var current = runtimeType; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current;
        }

        foreach (var interfaceType in EnumerateInterfacesMostSpecificFirst(runtimeType))
        {
            yield return interfaceType;
        }

        yield return typeof(object);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Runtime event dispatch inspects implemented interfaces by design.")]
    private static IEnumerable<Type> EnumerateInterfacesMostSpecificFirst(Type runtimeType)
    {
        var interfaces = runtimeType.GetInterfaces();
        return interfaces
            .OrderBy(interfaceType => interfaces.Count(other => other != interfaceType && interfaceType.IsAssignableFrom(other)))
            .ThenBy(interfaceType => interfaceType.FullName, StringComparer.Ordinal);
    }
}

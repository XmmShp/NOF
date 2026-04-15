using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace NOF.Abstraction;

/// <summary>
/// Stores source-generated in-memory event handler metadata.
/// </summary>
public static class EventHandlerRegistry
{
    private static readonly ConcurrentBag<EventHandlerRegistration> _registrations = new();

    public static void Register(EventHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations.Add(registration);
    }

    public static IReadOnlyList<EventHandlerRegistration> GetRegistrations()
    {
        if (_registrations.IsEmpty)
        {
            return [];
        }

        return new ReadOnlyCollection<EventHandlerRegistration>(_registrations.ToArray());
    }
}

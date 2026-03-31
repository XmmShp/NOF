using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace NOF.Application;

/// <summary>
/// Stores source-generated handler metadata.
/// </summary>
public static class HandlerRegistry
{
    private static readonly ConcurrentBag<HandlerInfo> Registrations = new();

    /// <summary>
    /// Adds one source-generated handler registration entry.
    /// </summary>
    public static void Register(HandlerInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Registrations.Add(info);
    }

    /// <summary>
    /// Gets all source-generated handler registrations.
    /// </summary>
    public static IReadOnlyList<HandlerInfo> GetRegistrations()
    {
        if (Registrations.IsEmpty)
        {
            return [];
        }

        return new ReadOnlyCollection<HandlerInfo>(Registrations.ToArray());
    }
}

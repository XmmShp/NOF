using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NOF.Application;

/// <summary>
/// Stores source-generated RPC request handler registrations.
/// </summary>
public static class RequestHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, Type> Registrations = new();

    /// <summary>
    /// Adds one source-generated request handler registration entry.
    /// </summary>
    public static void Register(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (Registrations.TryAdd(serviceType, implementationType))
        {
            return;
        }

        if (Registrations.TryGetValue(serviceType, out var existing) && existing != implementationType)
        {
            throw new InvalidOperationException(
                $"Conflicting RPC request handler registrations for '{serviceType.FullName}': '{existing.FullName}' and '{implementationType.FullName}'.");
        }
    }

    /// <summary>
    /// Gets all source-generated request handler registrations.
    /// </summary>
    public static IReadOnlyList<RequestHandlerRegistration> GetRegistrations()
    {
        if (Registrations.IsEmpty)
        {
            return [];
        }

        var items = Registrations
            .Select(kvp => new RequestHandlerRegistration(kvp.Key, kvp.Value))
            .ToArray();
        return new ReadOnlyCollection<RequestHandlerRegistration>(items);
    }
}

/// <summary>
/// Represents one request handler registration item.
/// </summary>
public sealed record RequestHandlerRegistration(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type ImplementationType);

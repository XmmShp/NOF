using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

/// <summary>
/// Stores source-generated RPC server registrations.
/// </summary>
public static class RpcServerRegistry
{
    private static readonly ConcurrentDictionary<Type, Type> Registrations = new();

    /// <summary>
    /// Registers one RPC contract to server implementation mapping.
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
                $"Conflicting RPC server registrations for '{serviceType.FullName}': '{existing.FullName}' and '{implementationType.FullName}'.");
        }
    }

    /// <summary>
    /// Tries to get the concrete server implementation type for one service contract.
    /// </summary>
    public static bool TryGetImplementationType(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        out Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return Registrations.TryGetValue(serviceType, out implementationType!);
    }

    /// <summary>
    /// Gets all registered server mappings.
    /// </summary>
    public static IReadOnlyList<RpcServerRegistration> GetRegistrations()
    {
        if (Registrations.IsEmpty)
        {
            return [];
        }

        var items = Registrations
            .Select(kvp => new RpcServerRegistration(kvp.Key, kvp.Value))
            .ToArray();
        return new ReadOnlyCollection<RpcServerRegistration>(items);
    }
}

/// <summary>
/// Represents one RPC server registration item.
/// </summary>
public sealed record RpcServerRegistration(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type ImplementationType);

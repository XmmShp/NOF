using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Annotation;

/// <summary>
/// Stores source-generated <see cref="AutoInjectAttribute"/> metadata.
/// </summary>
public static class AutoInjectRegistry
{
    private static readonly ConcurrentBag<AutoInjectServiceRegistration> Registrations = new();

    /// <summary>
    /// Adds one source-generated service registration entry.
    /// </summary>
    public static void Register(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type implementationType,
        Lifetime lifetime,
        bool useFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        Registrations.Add(new AutoInjectServiceRegistration(serviceType, implementationType, lifetime, useFactory));
    }

    /// <summary>
    /// Gets all source-generated auto-inject registrations.
    /// </summary>
    public static IReadOnlyList<AutoInjectServiceRegistration> GetRegistrations()
    {
        if (Registrations.IsEmpty)
        {
            return [];
        }

        return new ReadOnlyCollection<AutoInjectServiceRegistration>(Registrations.ToArray());
    }
}

/// <summary>
/// Represents one source-generated auto-inject registration item.
/// </summary>
public sealed record AutoInjectServiceRegistration(
    Type ServiceType,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type ImplementationType,
    Lifetime Lifetime,
    bool UseFactory);

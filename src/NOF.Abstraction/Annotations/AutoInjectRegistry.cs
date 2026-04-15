using System.Collections.Concurrent;
using NOF.Abstraction;

namespace NOF.Annotation;

/// <summary>
/// Stores source-generated <see cref="AutoInjectAttribute"/> metadata.
/// </summary>
public static class AutoInjectRegistry
{
    public static ConcurrentBag<AutoInjectServiceRegistration> Registrations => Registry.AutoInjectRegistrations;
}

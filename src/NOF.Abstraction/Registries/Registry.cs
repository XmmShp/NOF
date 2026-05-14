using NOF.Annotation;
using System.Collections.Concurrent;

namespace NOF.Abstraction;

/// <summary>
/// Builder-scoped storage for source-generated registration metadata.
/// </summary>
public sealed class Registry
{
    private readonly ConcurrentDictionary<string, object> _items = new(StringComparer.Ordinal);

    public ConcurrentDictionary<Type, bool> IsInitialized { get; } = new();

    public T GetOrAdd<T>(string key, Func<T> factory)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        return (T)_items.GetOrAdd(key, static (_, currentFactory) => currentFactory(), factory);
    }
}

public static partial class RegistryExtensions
{
    private const string EventHandlerRegistrationsKey = "NOF.Abstraction.EventHandlerRegistrations";
    private const string AutoInjectRegistrationsKey = "NOF.Abstraction.AutoInjectRegistrations";

    extension(Registry registry)
    {
        public List<EventHandlerRegistration> EventHandlerRegistrations
            => registry.GetOrAdd(EventHandlerRegistrationsKey, static () => new List<EventHandlerRegistration>());

        public List<AutoInjectServiceRegistration> AutoInjectRegistrations
            => registry.GetOrAdd(AutoInjectRegistrationsKey, static () => new List<AutoInjectServiceRegistration>());
    }
}

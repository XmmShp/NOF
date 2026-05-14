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
    private const string EventHandlerRegistryKey = "NOF.Abstraction.EventHandlerRegistry";
    private const string AutoInjectRegistryKey = "NOF.Abstraction.AutoInjectRegistry";

    extension(Registry registry)
    {
        public EventHandlerRegistry EventHandlerRegistry
            => registry.GetOrAdd(EventHandlerRegistryKey, static () => new EventHandlerRegistry());

        public AutoInjectRegistry AutoInjectRegistry
            => registry.GetOrAdd(AutoInjectRegistryKey, static () => new AutoInjectRegistry());
    }
}

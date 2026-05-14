using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace NOF.Abstraction;

/// <summary>
/// Builder-scoped storage for source-generated registration metadata.
/// </summary>
public sealed class Registry
{
    private readonly ConcurrentDictionary<string, object> _items = new(StringComparer.Ordinal);

    public Registry()
    {
        AutoInjectRegistry = new AutoInjectRegistry();
        RegisterSingletonAutoInject(this);
        RegisterSingletonAutoInject(AutoInjectRegistry);
    }

    public ConcurrentDictionary<Type, bool> IsInitialized { get; } = new();
    public AutoInjectRegistry AutoInjectRegistry { get; }

    public T GetOrAdd<T>(string key, Func<T> factory)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        while (true)
        {
            if (_items.TryGetValue(key, out var existing))
            {
                return (T)existing;
            }

            var created = factory();
            if (!_items.TryAdd(key, created))
            {
                continue;
            }

            RegisterSingletonAutoInject(created);
            return created;
        }
    }

    private void RegisterSingletonAutoInject<T>(T instance)
        where T : class
    {
        AutoInjectRegistry.Add(ServiceDescriptor.Singleton(instance));
    }
}

public static partial class RegistryExtensions
{
    private const string EventHandlerRegistryKey = "NOF.Abstraction.EventHandlerRegistry";

    extension(Registry registry)
    {
        public EventHandlerRegistry EventHandlerRegistry
            => registry.GetOrAdd(EventHandlerRegistryKey, static () => new EventHandlerRegistry());
    }
}

using System.Collections.Concurrent;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Stores endpoint name mappings keyed by <see cref="Type"/>.
/// <para>
/// Populated at startup by source-generated <c>AddAllHandlers</c> and optionally
/// overridden via <see cref="HandlerSelector"/> fluent API.
/// </para>
/// <para>
/// Registered as a singleton in DI via <c>GetOrAddSingleton</c>.
/// </para>
/// </summary>
public sealed class EndpointNameRegistry : ConcurrentDictionary<Type, string>
{
    /// <summary>
    /// Sets the endpoint name for the specified type, overwriting any existing entry.
    /// </summary>
    public EndpointNameRegistry Set<T>(string endpointName)
        => Set(typeof(T), endpointName);

    /// <summary>
    /// Sets the endpoint name for the specified type, overwriting any existing entry.
    /// </summary>
    public EndpointNameRegistry Set(Type type, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(endpointName);
        this[type] = endpointName;
        return this;
    }

    /// <summary>
    /// Sets the endpoint name only if no entry exists for this type.
    /// Returns <see langword="true"/> if the name was added.
    /// </summary>
    public bool TrySet<T>(string endpointName)
        => TrySet(typeof(T), endpointName);

    /// <summary>
    /// Sets the endpoint name only if no entry exists for this type.
    /// Returns <see langword="true"/> if the name was added.
    /// </summary>
    public bool TrySet(Type type, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(endpointName);
        return TryAdd(type, endpointName);
    }
}

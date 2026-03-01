using System.Collections.Concurrent;

namespace NOF.Application;

/// <summary>
/// Stores explicitly registered mapping functions and implements <see cref="IMapper"/>.
/// <para>
/// Thread-safe. Duplicate registrations for the same <c>(TSource, TDestination)</c> pair
/// are silently ignored (first-wins), so callers may safely register mappings in hot paths
/// (e.g. handler constructors) without extra allocation or GC pressure.
/// </para>
/// <para>
/// Designed to be used as a singleton via <c>services.GetOrAddSingleton&lt;ManualMapper&gt;()</c>
/// so that mappings can be configured before the DI container is built.
/// </para>
/// </summary>
public sealed class ManualMapper : IMapper
{
    private readonly ConcurrentDictionary<(Type Source, Type Destination), Delegate> _maps = new();

    /// <inheritdoc />
    public IMapper CreateMap<TSource, TDestination>(Func<TSource, TDestination> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        _maps.TryAdd((typeof(TSource), typeof(TDestination)), mappingFunc);
        return this;
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (_maps.TryGetValue((typeof(TSource), typeof(TDestination)), out var del))
        {
            return ((Func<TSource, TDestination>)del)(source);
        }

        throw new InvalidOperationException(
            $"No mapping registered from {typeof(TSource).FullName} to {typeof(TDestination).FullName}. " +
            "Call IMapper.CreateMap<TSource, TDestination>() to register a mapping.");
    }

    /// <inheritdoc />
    public bool TryMap<TSource, TDestination>(TSource source, out TDestination? result)
    {
        if (_maps.TryGetValue((typeof(TSource), typeof(TDestination)), out var del))
        {
            result = ((Func<TSource, TDestination>)del)(source);
            return true;
        }

        result = default;
        return false;
    }
}

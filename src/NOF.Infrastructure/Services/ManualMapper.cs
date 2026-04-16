using NOF.Application;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

/// <summary>
/// Implements <see cref="IMapper"/> using explicitly registered mapping functions.
/// <para>
/// Thread-safe. Each <see cref="MapKey"/> holds exactly one delegate.
/// </para>
/// <para>
/// Mapping lookup priority: exact type pair - open generic source - open generic dest
/// - open generic both - <c>Nullable&lt;T&gt;</c> fallback (<c>A - T?</c> uses <c>A - T</c>).
/// </para>
/// </summary>
public sealed class ManualMapper : IMapper
{
    private readonly ConcurrentDictionary<MapKey, MapFunc> _mappings = new();

    /// <summary>
    /// Creates a new <see cref="ManualMapper"/> seeded with mappings from the global registry.
    /// </summary>
    public ManualMapper(MapperInfos mapperInfos)
    {
        foreach (var kvp in mapperInfos.Mappings)
        {
            _mappings[kvp.Key] = kvp.Value;
        }
    }

    #region Generic mapping

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null)
    {
        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        var func = ResolveDelegate(sourceType, typeof(TDestination), name);
        if (func is not null)
        {
            return (TDestination)func(source!, this);
        }

        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {typeof(TDestination).FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    /// <inheritdoc />
    public bool TryMap<TSource, TDestination>(TSource source, out TDestination result, bool useRuntimeType = false, string? name = null)
    {
        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        var func = ResolveDelegate(sourceType, typeof(TDestination), name);
        if (func is not null)
        {
            result = (TDestination)func(source!, this);
            return true;
        }

        result = default!;
        return false;
    }

    #endregion

    #region Non-generic mapping

    /// <inheritdoc />
    public object Map(Type sourceType, Type destinationType, object source, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(source);

        var func = ResolveDelegate(sourceType, destinationType, name);
        if (func is not null)
        {
            return func(source, this);
        }

        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {destinationType.FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    /// <inheritdoc />
    public bool TryMap(Type sourceType, Type destinationType, object source, out object? result, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(source);

        var func = ResolveDelegate(sourceType, destinationType, name);
        if (func is not null)
        {
            result = func(source, this);
            return true;
        }

        result = null;
        return false;
    }

    #endregion

    #region Lookup: exact - open generic - Nullable<T> fallback

    private MapFunc? ResolveDelegate(Type sourceType, Type destType, string? name)
    {
        while (true)
        {
            var key = new MapKey(sourceType, destType, name);
            if (_mappings.TryGetValue(key, out var func))
            {
                return func;
            }

            // Open generic fallback
            var openSource = sourceType.IsGenericType ? sourceType.GetGenericTypeDefinition() : null;
            var openDest = destType.IsGenericType ? destType.GetGenericTypeDefinition() : null;

            if (openSource is not null)
            {
                key = new MapKey(openSource, destType, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openDest is not null)
            {
                key = new MapKey(sourceType, openDest, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openSource is not null && openDest is not null)
            {
                key = new MapKey(openSource, openDest, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            // Nullable<T> fallback: A - T? can use A - T mapping (but not vice versa)
            var underlyingDest = Nullable.GetUnderlyingType(destType);
            if (underlyingDest is not null)
            {
                destType = underlyingDest;
                continue;
            }

            return null;
        }
    }

    #endregion
}

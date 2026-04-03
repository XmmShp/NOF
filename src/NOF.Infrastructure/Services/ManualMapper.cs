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
    public ManualMapper()
    {
        foreach (var kvp in MapperRegistry.GetRegistrationsSnapshot())
        {
            _mappings[kvp.Key] = kvp.Value;
        }
    }

    #region Generic registration

    /// <inheritdoc />
    public IMapper Add<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        _mappings[key] = (src, _) => mappingFunc((TSource)src)!;
        return this;
    }

    /// <inheritdoc />
    public IMapper Add<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        _mappings[key] = (src, mapper) => mappingFunc((TSource)src, mapper)!;
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        return _mappings.TryAdd(key, (src, _) => mappingFunc((TSource)src)!);
    }

    /// <inheritdoc />
    public bool TryAdd<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(typeof(TSource), typeof(TDestination), name);
        return _mappings.TryAdd(key, (src, mapper) => mappingFunc((TSource)src, mapper)!);
    }

    #endregion

    #region Non-generic registration

    /// <inheritdoc />
    public IMapper Add(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(sourceType, destinationType, name);
        _mappings[key] = mappingFunc;
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var key = new MapKey(sourceType, destinationType, name);
        return _mappings.TryAdd(key, mappingFunc);
    }

    #endregion

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

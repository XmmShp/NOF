using Microsoft.Extensions.Options;
using NOF.Application;

namespace NOF.Infrastructure;

/// <summary>
/// Implements <see cref="IMapper"/> using explicitly registered mapping functions.
/// <para>
/// Thread-safe. Each <see cref="MapKey"/> holds exactly one delegate.
/// </para>
/// <para>
/// Mapping lookup priority: exact type pair â†?open generic source â†?open generic dest
/// â†?open generic both â†?<c>Nullable&lt;T&gt;</c> fallback (<c>A â†?T?</c> uses <c>A â†?T</c>).
/// </para>
/// </summary>
public sealed class ManualMapper : IMapper
{
    private readonly MapperOptions _options;

    /// <summary>
    /// Creates a new <see cref="ManualMapper"/> seeded with mappings from <paramref name="options"/>.
    /// </summary>
    public ManualMapper(IOptions<MapperOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    #region Generic registration

    /// <inheritdoc />
    public IMapper Add<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        _options.Add(mappingFunc, name);
        return this;
    }

    /// <inheritdoc />
    public IMapper Add<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        _options.Add(mappingFunc, name);
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
        => _options.TryAdd(mappingFunc, name);

    /// <inheritdoc />
    public bool TryAdd<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
        => _options.TryAdd(mappingFunc, name);

    #endregion

    #region Non-generic registration

    /// <inheritdoc />
    public IMapper Add(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        _options.Add(sourceType, destinationType, mappingFunc, name);
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
        => _options.TryAdd(sourceType, destinationType, mappingFunc, name);

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

    #region Lookup: exact â†?open generic â†?Nullable<T> fallback

    private MapFunc? ResolveDelegate(Type sourceType, Type destType, string? name)
    {
        while (true)
        {
            var key = new MapKey(sourceType, destType, name);
            if (_options.TryGetValue(key, out var func))
            {
                return func;
            }

            // Open generic fallback
            var openSource = sourceType.IsGenericType ? sourceType.GetGenericTypeDefinition() : null;
            var openDest = destType.IsGenericType ? destType.GetGenericTypeDefinition() : null;

            if (openSource is not null)
            {
                key = new MapKey(openSource, destType, name);
                if (_options.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openDest is not null)
            {
                key = new MapKey(sourceType, openDest, name);
                if (_options.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openSource is not null && openDest is not null)
            {
                key = new MapKey(openSource, openDest, name);
                if (_options.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            // Nullable<T> fallback: A â†?T? can use A â†?T mapping (but not vice versa)
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

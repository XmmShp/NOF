using Microsoft.Extensions.Options;
using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Implements <see cref="IMapper"/> using explicitly registered mapping functions.
/// <para>
/// Thread-safe. Each <see cref="MapKey"/> may hold multiple delegates; during mapping
/// they are invoked in reverse-registration order (last-added first) and the first
/// result with <see cref="Optional{T}.HasValue"/> wins.
/// </para>
/// <para>
/// Mapping lookup priority: closed type → open generic definition.
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
    public IMapper Add<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
    {
        _options.Add(mappingFunc, name);
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
        => _options.TryAdd(mappingFunc, name);

    /// <inheritdoc />
    public IMapper ReplaceOrAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
    {
        _options.ReplaceOrAdd(mappingFunc, name);
        return this;
    }

    #endregion

    #region Non-generic registration

    /// <inheritdoc />
    public IMapper Add(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
    {
        _options.Add(sourceType, destinationType, mappingFunc, name);
        return this;
    }

    /// <inheritdoc />
    public bool TryAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
        => _options.TryAdd(sourceType, destinationType, mappingFunc, name);

    /// <inheritdoc />
    public IMapper ReplaceOrAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
    {
        _options.ReplaceOrAdd(sourceType, destinationType, mappingFunc, name);
        return this;
    }

    #endregion

    #region Generic mapping

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null)
    {
        var result = TryMap<TSource, TDestination>(source, useRuntimeType, name);
        if (result.HasValue)
        {
            return result.Value;
        }

        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {typeof(TDestination).FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    /// <inheritdoc />
    public Optional<TDestination> TryMap<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null)
    {
        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        var delegates = ResolveDelegates(sourceType, typeof(TDestination), name);
        if (delegates is not null)
        {
            // Iterate in reverse: last-added first
            for (var i = delegates.Count - 1; i >= 0; i--)
            {
                var result = delegates[i](source!);
                if (result.HasValue)
                {
                    return (TDestination)result.Value!;
                }
            }
        }

        // Built-in fallback (unnamed mappings only)
        if (name is null && source is not null)
        {
            var builtIn = BuiltInMappings.TryMap(sourceType, typeof(TDestination), source);
            if (builtIn.HasValue)
            {
                return (TDestination)builtIn.Value!;
            }
        }

        return Optional.None;
    }

    #endregion

    #region Non-generic mapping

    /// <inheritdoc />
    public object Map(Type sourceType, Type destinationType, object source, string? name = null)
    {
        var result = TryMap(sourceType, destinationType, source, name);
        if (result.HasValue)
        {
            return result.Value!;
        }

        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {destinationType.FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    /// <inheritdoc />
    public Optional<object> TryMap(Type sourceType, Type destinationType, object source, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(source);

        var delegates = ResolveDelegates(sourceType, destinationType, name);
        if (delegates is not null)
        {
            for (var i = delegates.Count - 1; i >= 0; i--)
            {
                var result = delegates[i](source);
                if (result.HasValue)
                {
                    return Optional.Of(result.Value!);
                }
            }
        }

        // Built-in fallback (unnamed mappings only)
        if (name is null)
        {
            var builtIn = BuiltInMappings.TryMap(sourceType, destinationType, source);
            if (builtIn.HasValue)
            {
                return Optional.Of(builtIn.Value!);
            }
        }

        return Optional.None;
    }

    #endregion

    #region Lookup: closed type first, then open generic definition

    private List<Func<object, Optional<object?>>>? ResolveDelegates(Type sourceType, Type destType, string? name)
    {
        while (true)
        {
            var key = new MapKey(sourceType, destType, name);
            if (_options.TryGetValue(key, out var delegates))
            {
                return delegates;
            }

            // Open generic fallback
            var openSource = sourceType.IsGenericType ? sourceType.GetGenericTypeDefinition() : null;
            var openDest = destType.IsGenericType ? destType.GetGenericTypeDefinition() : null;

            if (openSource is not null)
            {
                key = new MapKey(openSource, destType, name);
                if (_options.TryGetValue(key, out delegates))
                {
                    return delegates;
                }
            }

            if (openDest is not null)
            {
                key = new MapKey(sourceType, openDest, name);
                if (_options.TryGetValue(key, out delegates))
                {
                    return delegates;
                }
            }

            if (openSource is not null && openDest is not null)
            {
                key = new MapKey(openSource, openDest, name);
                if (_options.TryGetValue(key, out delegates))
                {
                    return delegates;
                }
            }

            // Nullable<T> fallback: A → T? can use A → T mapping (but not vice versa)
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

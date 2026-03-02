using System.Collections.Concurrent;

namespace NOF.Application;

/// <summary>
/// Mapping delegate type. Receives the source object and the <see cref="IMapper"/> (for nested mappings),
/// returns the mapped destination object.
/// </summary>
public delegate object MapFunc(object source, IMapper mapper);

/// <summary>
/// Stores mapping delegates keyed by <see cref="MapKey"/>.
/// Each key holds exactly one delegate — later registrations replace earlier ones
/// unless <see cref="TryAdd(Type, Type, MapFunc, string?)"/> is used.
/// <para>
/// No built-in mappings are provided. All mappings must be explicitly registered.
/// The only implicit behavior is <c>Nullable&lt;T&gt;</c> fallback: a mapping
/// <c>A → T</c> is automatically used for <c>A → T?</c> when no direct registration exists.
/// </para>
/// <para>
/// Use <c>services.Configure&lt;MapperOptions&gt;(o =&gt; o.Add&lt;A, B&gt;(...))</c>
/// to register mappings at configuration time.
/// </para>
/// </summary>
public sealed class MapperOptions : ConcurrentDictionary<MapKey, MapFunc>
{
    #region Generic registration

    /// <summary>
    /// Registers a mapping delegate for the given type pair and optional name.
    /// If a delegate already exists for this key, it is replaced.
    /// </summary>
    public MapperOptions Add<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return Add(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    /// <summary>
    /// Registers a mapping delegate that also receives an <see cref="IMapper"/> for nested mappings.
    /// </summary>
    public MapperOptions Add<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return Add(typeof(TSource), typeof(TDestination), WrapGenericWithMapper(mappingFunc), name);
    }

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    public bool TryAdd<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return TryAdd(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    /// <inheritdoc cref="TryAdd{TSource, TDestination}(Func{TSource, TDestination}, string?)"/>
    public bool TryAdd<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return TryAdd(typeof(TSource), typeof(TDestination), WrapGenericWithMapper(mappingFunc), name);
    }


    #endregion

    #region Non-generic registration

    /// <summary>
    /// Registers a non-generic mapping delegate. Replaces any existing delegate for this key.
    /// </summary>
    public MapperOptions Add(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        this[new MapKey(sourceType, destinationType, name)] = mappingFunc;
        return this;
    }

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    public bool TryAdd(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        return TryAdd(new MapKey(sourceType, destinationType, name), mappingFunc);
    }


    #endregion

    #region Merge

    /// <summary>
    /// Merges all mappings from <paramref name="other"/> into this instance.
    /// Existing keys in this instance are <em>not</em> overwritten; only new keys are added.
    /// </summary>
    public MapperOptions Merge(MapperOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var (key, func) in other)
        {
            TryAdd(key, func);
        }
        return this;
    }

    #endregion

    #region Helpers

    private static MapFunc WrapGeneric<TSource, TDestination>(Func<TSource, TDestination> mappingFunc)
    {
        return (src, mapper) => mappingFunc((TSource)src)!;
    }

    private static MapFunc WrapGenericWithMapper<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc)
    {
        return (src, mapper) => mappingFunc((TSource)src, mapper)!;
    }

    #endregion
}

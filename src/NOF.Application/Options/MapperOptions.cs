using NOF.Contract;
using System.Collections.Concurrent;

namespace NOF.Application;

/// <summary>
/// Stores mapping delegate lists keyed by <see cref="MapKey"/>.
/// Inherits <see cref="ConcurrentDictionary{TKey, TValue}"/> for direct dictionary access.
/// <para>
/// Each key may hold multiple delegates. During mapping, delegates are invoked in
/// reverse-registration order (last-added first); the first non-<see langword="null"/> result wins.
/// </para>
/// <para>
/// Use <c>services.Configure&lt;MapperOptions&gt;(o =&gt; o.Add&lt;A, B&gt;(...))</c>
/// to register mappings at configuration time.
/// </para>
/// </summary>
public sealed class MapperOptions : ConcurrentDictionary<MapKey, List<Func<object, Optional<object?>>>>
{
    #region Generic registration

    /// <summary>
    /// Appends a mapping delegate for the given type pair and name.
    /// Later-added delegates are evaluated first during mapping.
    /// </summary>
    public MapperOptions Add<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return Add(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    public bool TryAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return TryAdd(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    /// <summary>
    /// Clears all existing delegates for this key and registers a single new one.
    /// </summary>
    public MapperOptions ReplaceOrAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return ReplaceOrAdd(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    #endregion

    #region Non-generic registration

    /// <summary>
    /// Appends a non-generic mapping delegate.
    /// </summary>
    public MapperOptions Add(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        var key = new MapKey(sourceType, destinationType, name);
        AddOrUpdate(key,
            static (_, f) => [f],
            static (_, list, f) => { list.Add(f); return list; },
            mappingFunc);
        return this;
    }

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    public bool TryAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        var key = new MapKey(sourceType, destinationType, name);
        return TryAdd(key, [mappingFunc]);
    }

    /// <summary>
    /// Clears all existing delegates for this key and registers a single new one.
    /// </summary>
    public MapperOptions ReplaceOrAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        var key = new MapKey(sourceType, destinationType, name);
        this[key] = [mappingFunc];
        return this;
    }

    #endregion

    #region Merge

    /// <summary>
    /// Merges all mappings from <paramref name="other"/> into this instance.
    /// For each key, delegates from <paramref name="other"/> are appended after existing ones
    /// (thus evaluated with lower priority).
    /// </summary>
    public MapperOptions Merge(MapperOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var (key, delegates) in other)
        {
            AddOrUpdate(key,
                static (_, d) => new List<Func<object, Optional<object?>>>(d),
                static (_, existing, d) => { existing.AddRange(d); return existing; },
                delegates);
        }
        return this;
    }

    #endregion

    #region Helpers

    private static Func<object, Optional<object?>> WrapGeneric<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc)
    {
        return src =>
        {
            var result = mappingFunc((TSource)src);
            return result.HasValue ? Optional.Of<object?>(result.Value) : Optional.None;
        };
    }

    #endregion
}

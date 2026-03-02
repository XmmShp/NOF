namespace NOF.Application;

/// <summary>
/// Maps an object of one type to another.
/// All mappings must be explicitly registered — no built-in or implicit mappings are provided.
/// <para>
/// Each <see cref="MapKey"/> holds exactly one delegate. The only implicit behavior is
/// <c>Nullable&lt;T&gt;</c> fallback: a mapping <c>A → T</c> is used for <c>A → T?</c>
/// when no direct registration exists.
/// </para>
/// </summary>
public interface IMapper
{
    #region Generic registration

    /// <summary>
    /// Registers a mapping delegate. Replaces any existing delegate for this key.
    /// </summary>
    IMapper Add<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null);

    /// <summary>
    /// Registers a mapping delegate that also receives an <see cref="IMapper"/> for nested mappings.
    /// </summary>
    IMapper Add<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null);

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    bool TryAdd<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null);

    /// <inheritdoc cref="TryAdd{TSource, TDestination}(Func{TSource, TDestination}, string?)"/>
    bool TryAdd<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null);

    #endregion

    #region Non-generic registration

    /// <summary>
    /// Registers a non-generic mapping delegate. Replaces any existing delegate for this key.
    /// </summary>
    IMapper Add(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null);

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// Returns <see langword="true"/> if the delegate was added.
    /// </summary>
    bool TryAdd(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null);

    #endregion

    #region Generic mapping

    /// <summary>
    /// Maps <paramref name="source"/> to <typeparamref name="TDestination"/>.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="useRuntimeType">
    /// When <see langword="true"/>, source type for lookup is <c>source.GetType()</c>
    /// rather than <typeparamref name="TSource"/>.
    /// </param>
    /// <param name="name">Optional mapping name.</param>
    /// <exception cref="InvalidOperationException">No mapping registered for this key.</exception>
    TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null);

    /// <summary>
    /// Attempts to map <paramref name="source"/>.
    /// Returns <see langword="true"/> if a mapping was found and <paramref name="result"/> contains the mapped value.
    /// </summary>
    bool TryMap<TSource, TDestination>(TSource source, out TDestination result, bool useRuntimeType = false, string? name = null);

    #endregion

    #region Non-generic mapping

    /// <summary>
    /// Maps <paramref name="source"/> to <paramref name="destinationType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No mapping registered for this key.</exception>
    object Map(Type sourceType, Type destinationType, object source, string? name = null);

    /// <summary>
    /// Attempts a non-generic map.
    /// Returns <see langword="true"/> if a mapping was found and <paramref name="result"/> contains the mapped value.
    /// </summary>
    bool TryMap(Type sourceType, Type destinationType, object source, out object? result, string? name = null);

    #endregion
}

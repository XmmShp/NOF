using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Maps an object of one type to another.
/// All mappings must be explicitly registered — no reflection is used.
/// <para>
/// Each <see cref="MapKey"/> may hold multiple delegates. During mapping, delegates are
/// invoked in reverse-registration order (last-added first); the first that returns a
/// value with <see cref="Optional{T}.HasValue"/> == <see langword="true"/> wins.
/// </para>
/// </summary>
public interface IMapper
{
    #region Generic registration

    /// <summary>
    /// Appends a mapping delegate. Later-added delegates are evaluated first.
    /// </summary>
    IMapper Add<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null);

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// </summary>
    bool TryAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null);

    /// <summary>
    /// Clears all existing delegates for this key and registers a single new one.
    /// </summary>
    IMapper ReplaceOrAdd<TSource, TDestination>(Func<TSource, Optional<TDestination>> mappingFunc, string? name = null);

    #endregion

    #region Non-generic registration

    /// <summary>
    /// Appends a non-generic mapping delegate.
    /// </summary>
    IMapper Add(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null);

    /// <summary>
    /// Adds the delegate only if no delegate has been registered for this key yet.
    /// </summary>
    bool TryAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null);

    /// <summary>
    /// Clears all existing delegates for this key and registers a single new one.
    /// </summary>
    IMapper ReplaceOrAdd(Type sourceType, Type destinationType, Func<object, Optional<object?>> mappingFunc, string? name = null);

    #endregion

    #region Generic mapping

    /// <summary>
    /// Maps <paramref name="source"/> to <typeparamref name="TDestination"/>.
    /// Iterates registered delegates (last-added first) and returns the first non-empty result.
    /// </summary>
    /// <param name="useRuntimeType">
    /// When <see langword="true"/>, source type for lookup is <c>source.GetType()</c>
    /// rather than <typeparamref name="TSource"/>.
    /// </param>
    /// <param name="name">Optional mapping name.</param>
    /// <exception cref="InvalidOperationException">No mapping produced a value.</exception>
    TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null);

    /// <summary>
    /// Attempts to map <paramref name="source"/>. Returns the first non-empty result.
    /// </summary>
    Optional<TDestination> TryMap<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null);

    #endregion

    #region Non-generic mapping

    /// <summary>
    /// Maps <paramref name="source"/> to <paramref name="destinationType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No mapping produced a value.</exception>
    object Map(Type sourceType, Type destinationType, object source, string? name = null);

    /// <summary>
    /// Attempts a non-generic map. Returns the first non-empty result.
    /// </summary>
    Optional<object> TryMap(Type sourceType, Type destinationType, object source, string? name = null);

    #endregion
}

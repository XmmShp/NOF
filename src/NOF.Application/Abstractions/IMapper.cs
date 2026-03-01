namespace NOF.Application;

/// <summary>
/// Maps an object of one type to another.
/// All mappings must be explicitly registered — no reflection is used.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Registers a mapping function from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.
    /// If a mapping for the same pair already exists, this call is a no-op (first-wins semantics),
    /// so callers may safely register mappings in hot paths (e.g. handler constructors)
    /// without extra allocation or GC pressure.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mappingFunc">The mapping function.</param>
    /// <returns>This instance for chaining.</returns>
    IMapper CreateMap<TSource, TDestination>(Func<TSource, TDestination> mappingFunc);

    /// <summary>
    /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>
    /// using a previously registered mapping function.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object to map from.</param>
    /// <returns>A new instance of <typeparamref name="TDestination"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no mapping from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/> has been registered.
    /// </exception>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Attempts to map <paramref name="source"/> using a registered mapping function.
    /// Returns <see langword="true"/> if a mapping was found and applied; otherwise <see langword="false"/>.
    /// </summary>
    bool TryMap<TSource, TDestination>(TSource source, out TDestination? result);

    /// <summary>
    /// Maps <paramref name="source"/> using an existing registered mapping if available;
    /// otherwise registers <paramref name="mappingFunc"/> first (first-wins), then applies it.
    /// <para>
    /// This is ideal for handler constructors where the caller wants to ensure a mapping
    /// exists without caring whether it was already registered elsewhere.
    /// </para>
    /// </summary>
    TDestination MapOrCreate<TSource, TDestination>(TSource source, Func<TSource, TDestination> mappingFunc)
    {
        CreateMap(mappingFunc);
        return Map<TSource, TDestination>(source);
    }
}

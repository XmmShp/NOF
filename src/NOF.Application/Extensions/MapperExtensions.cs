namespace NOF.Application;

/// <summary>
/// Extension methods for the NOF.Application layer.
/// </summary>
public static partial class NOFApplicationExtensions
{
    extension<TSource>(TSource source) where TSource : notnull
    {
        /// <summary>
        /// Gets a <see cref="MapSelector{TSource}"/> bound to the current global <see cref="IMapper"/>
        /// instance, providing fluent mapping via <c>.To&lt;T&gt;()</c> and <c>.As&lt;T&gt;()</c>.
        /// The <see cref="IMapper"/> reference is captured at the point this property is accessed.
        /// </summary>
        public MapSelector<TSource> Map => new(source, Mapper.Current);
    }
}

/// <summary>
/// Fluent selector returned by the <c>.Map</c> extension property.
/// Provides <see cref="To{TDestination}"/> (registered mapping, fallback to cast)
/// and <see cref="As{TDestination}"/> (inheritance / unboxing cast only).
/// <para>
/// For user-defined implicit/explicit conversion operators, register them as mappings
/// via <see cref="IMapper.CreateMap{TSource, TDestination}"/>, e.g.
/// <c>mapper.CreateMap&lt;A, B&gt;(a =&gt; (B)a)</c>.
/// </para>
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
public readonly struct MapSelector<TSource> where TSource : notnull
{
    private readonly TSource _source;
    private readonly IMapper _mapper;

    internal MapSelector(TSource source, IMapper mapper)
    {
        _source = source;
        _mapper = mapper;
    }

    /// <summary>
    /// Maps to <typeparamref name="TDestination"/> using a registered mapping function.
    /// If no mapping is registered, falls back to a language-native cast (<see cref="As{TDestination}"/>).
    /// </summary>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>The mapped or cast result.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown if no registered mapping exists and the runtime cast also fails.
    /// </exception>
    public TDestination To<TDestination>()
    {
        if (_mapper.TryMap<TSource, TDestination>(_source, out var result))
        {
            return result!;
        }

        return (TDestination)(object)_source!;
    }

    /// <summary>
    /// Converts to <typeparamref name="TDestination"/> using only runtime-supported conversions
    /// (inheritance casts, interface casts, unboxing).
    /// No registered mapping functions are consulted.
    /// <para>
    /// This does <b>not</b> invoke user-defined implicit/explicit operators — those are
    /// compile-time constructs. To support them, register a mapping via
    /// <see cref="IMapper.CreateMap{TSource, TDestination}"/> and use <see cref="To{TDestination}"/> instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>The cast result.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown if the runtime cast fails.
    /// </exception>
    public TDestination As<TDestination>()
    {
        return (TDestination)(object)_source!;
    }
}

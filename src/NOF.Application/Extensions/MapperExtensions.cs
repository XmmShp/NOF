using NOF.Contract;

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
        /// instance, providing fluent mapping via <c>.As&lt;T&gt;()</c> and <c>.To&lt;T&gt;()</c>.
        /// </summary>
        public MapSelector<TSource> Map => new(source!, typeof(TSource), Mapper.Current);
    }
}

/// <summary>
/// Fluent selector returned by the <c>.Map</c> extension property.
/// <para>
/// <see cref="As{TNewSource}"/> changes the source type used for mapping lookup (returns a new selector).
/// <see cref="AsRuntime"/> switches to using the runtime type of the source object.
/// <see cref="To{TDestination}"/> performs the mapping (registered mapping, fallback to cast).
/// <see cref="To(Type, string?)"/> performs a non-generic mapping.
/// </para>
/// </summary>
/// <typeparam name="TSource">The compile-time source type.</typeparam>
public readonly struct MapSelector<TSource>
{
    private readonly object _source;
    private readonly Type _sourceType;
    private readonly IMapper _mapper;

    internal MapSelector(object source, Type sourceType, IMapper mapper)
    {
        _source = source;
        _sourceType = sourceType;
        _mapper = mapper;
    }

    /// <summary>
    /// Returns a new selector that uses <typeparamref name="TNewSource"/> as the source type
    /// for mapping lookup. The source object reference is unchanged.
    /// <para>Usage: <c>source.Map.As&lt;DerivedType&gt;().To&lt;TDest&gt;()</c></para>
    /// </summary>
    public MapSelector<TNewSource> As<TNewSource>()
    {
        return new MapSelector<TNewSource>(_source, typeof(TNewSource), _mapper);
    }

    /// <summary>
    /// Returns a new selector that uses <paramref name="newSourceType"/> as the source type
    /// for mapping lookup. The source object reference is unchanged.
    /// <para>Usage: <c>source.Map.As(typeof(Derived)).To&lt;TDest&gt;()</c></para>
    /// </summary>
    public MapSelector<TSource> As(Type newSourceType)
    {
        return new MapSelector<TSource>(_source, newSourceType, _mapper);
    }

    /// <summary>
    /// Returns a new selector that uses the runtime type (<c>source.GetType()</c>) for mapping lookup.
    /// <para>Usage: <c>source.Map.AsRuntime.To&lt;TDest&gt;()</c> or <c>source.Map.AsRuntime.To(someType)</c></para>
    /// </summary>
    public MapSelector<TSource> AsRuntime
        => new(_source, _source.GetType(), _mapper);

    /// <summary>
    /// Maps to <typeparamref name="TDestination"/> using a registered mapping.
    /// Falls back to a language-native cast if no mapping is found.
    /// </summary>
    /// <param name="name">Optional mapping name.</param>
    public TDestination To<TDestination>(string? name = null)
    {
        var result = _mapper.TryMap(_sourceType, typeof(TDestination), _source, name);
        if (result.HasValue)
        {
            return (TDestination)result.Value;
        }

        return (TDestination)_source;
    }

    /// <summary>
    /// Non-generic mapping to <paramref name="destinationType"/>.
    /// Falls back to a language-native cast if no mapping is found.
    /// </summary>
    /// <param name="destinationType">The destination type.</param>
    /// <param name="name">Optional mapping name.</param>
    public object To(Type destinationType, string? name = null)
    {
        var result = _mapper.TryMap(_sourceType, destinationType, _source, name);
        if (result.HasValue)
        {
            return result.Value;
        }

        return _source;
    }
}

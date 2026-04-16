namespace NOF.Application;

/// <summary>
/// Identifies a mapping registration: source type, destination type, and optional name.
/// </summary>
/// <param name="Source">The source type.</param>
/// <param name="Destination">The destination type.</param>
/// <param name="Name">Optional mapping name. <see langword="null"/> = default (unnamed) mapping.</param>
public sealed record MapKey(Type Source, Type Destination, string? Name = null);

public sealed record MapperRegistration(
    MapKey Key,
    MapFunc MappingFunc)
{
    public static MapperRegistration Of<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return new MapperRegistration(
            new MapKey(typeof(TSource), typeof(TDestination), name),
            (source, _) => mappingFunc((TSource)source)!);
    }

    public static MapperRegistration Of<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return new MapperRegistration(
            new MapKey(typeof(TSource), typeof(TDestination), name),
            (source, mapper) => mappingFunc((TSource)source, mapper)!);
    }
}

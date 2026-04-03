using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace NOF.Application;

public static class MapperRegistry
{
    private static readonly ConcurrentDictionary<MapKey, MapFunc> Registrations = new();

    public static void Register<TSource, TDestination>(Func<TSource, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        Register(typeof(TSource), typeof(TDestination), WrapGeneric(mappingFunc), name);
    }

    public static void Register<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        Register(typeof(TSource), typeof(TDestination), WrapGenericWithMapper(mappingFunc), name);
    }

    public static void Register(Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(mappingFunc);

        var key = new MapKey(sourceType, destinationType, name);
        Registrations[key] = mappingFunc;
    }

    public static IReadOnlyDictionary<MapKey, MapFunc> GetRegistrationsSnapshot()
    {
        if (Registrations.IsEmpty)
        {
            return new ReadOnlyDictionary<MapKey, MapFunc>(new Dictionary<MapKey, MapFunc>());
        }

        return new ReadOnlyDictionary<MapKey, MapFunc>(new Dictionary<MapKey, MapFunc>(Registrations));
    }

    private static MapFunc WrapGeneric<TSource, TDestination>(Func<TSource, TDestination> mappingFunc)
    {
        return (src, mapper) => mappingFunc((TSource)src)!;
    }

    private static MapFunc WrapGenericWithMapper<TSource, TDestination>(Func<TSource, IMapper, TDestination> mappingFunc)
    {
        return (src, mapper) => mappingFunc((TSource)src, mapper)!;
    }
}

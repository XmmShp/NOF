using System.Collections.Concurrent;

namespace NOF.Application;

/// <summary>
/// Implements <see cref="IMapper"/> using explicitly registered mapping functions.
/// </summary>
/// <remarks>
/// This is the default application-layer mapper implementation. Explicit <see cref="IMapper"/>
/// dependencies remain the primary runtime contract, while ambient mapper access is a convenience API.
/// </remarks>
public sealed class ManualMapper : IMapper
{
    private readonly ConcurrentDictionary<MapKey, MapFunc> _mappings = new();

    public ManualMapper(MapperRegistry mapperRegistry)
    {
        foreach (var registration in mapperRegistry.Freeze())
        {
            _mappings[registration.Key] = registration.MappingFunc;
        }
    }

    public TDestination Map<TSource, TDestination>(TSource source, bool useRuntimeType = false, string? name = null)
    {
        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        var func = ResolveDelegate(sourceType, typeof(TDestination), name);
        if (func is not null)
        {
            return (TDestination)func(source!, this);
        }

        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {typeof(TDestination).FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    public bool TryMap<TSource, TDestination>(TSource source, out TDestination result, bool useRuntimeType = false, string? name = null)
    {
        var sourceType = useRuntimeType && source is not null ? source.GetType() : typeof(TSource);
        var func = ResolveDelegate(sourceType, typeof(TDestination), name);
        if (func is not null)
        {
            result = (TDestination)func(source!, this);
            return true;
        }

        result = default!;
        return false;
    }

    public object Map(Type sourceType, Type destinationType, object source, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(source);

        var func = ResolveDelegate(sourceType, destinationType, name);
        if (func is not null)
        {
            return func(source, this);
        }

        throw new InvalidOperationException(
            $"No mapping registered from {sourceType.FullName} to {destinationType.FullName}" +
            (name is null ? "." : $" with name '{name}'."));
    }

    public bool TryMap(Type sourceType, Type destinationType, object source, out object? result, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        ArgumentNullException.ThrowIfNull(source);

        var func = ResolveDelegate(sourceType, destinationType, name);
        if (func is not null)
        {
            result = func(source, this);
            return true;
        }

        result = null;
        return false;
    }

    private MapFunc? ResolveDelegate(Type sourceType, Type destType, string? name)
    {
        while (true)
        {
            var key = new MapKey(sourceType, destType, name);
            if (_mappings.TryGetValue(key, out var func))
            {
                return func;
            }

            var openSource = sourceType.IsGenericType ? sourceType.GetGenericTypeDefinition() : null;
            var openDest = destType.IsGenericType ? destType.GetGenericTypeDefinition() : null;

            if (openSource is not null)
            {
                key = new MapKey(openSource, destType, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openDest is not null)
            {
                key = new MapKey(sourceType, openDest, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            if (openSource is not null && openDest is not null)
            {
                key = new MapKey(openSource, openDest, name);
                if (_mappings.TryGetValue(key, out func))
                {
                    return func;
                }
            }

            var underlyingDest = Nullable.GetUnderlyingType(destType);
            if (underlyingDest is not null)
            {
                destType = underlyingDest;
                continue;
            }

            return null;
        }
    }
}

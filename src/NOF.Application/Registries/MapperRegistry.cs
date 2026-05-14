using NOF.Abstraction;

namespace NOF.Application;

public sealed class MapperRegistry : Registry<MapperRegistration>
{
    private readonly Dictionary<MapKey, MapFunc> _mappings = [];

    public MapperRegistry()
    {
        Frozen += BuildIndexes;
    }

    public bool TryGet(MapKey key, out MapFunc? mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(key);
        Freeze();
        return _mappings.TryGetValue(key, out mappingFunc);
    }

    private void BuildIndexes()
    {
        _mappings.Clear();
        foreach (var registration in Items)
        {
            Index(registration);
        }
    }

    private void Index(MapperRegistration registration)
    {
        _mappings[registration.Key] = registration.MappingFunc;
    }
}

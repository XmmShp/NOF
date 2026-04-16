using NOF.Abstraction;

namespace NOF.Application;

public sealed class MapperInfos
{
    private readonly Lock _gate = new();
    private readonly Dictionary<MapKey, MapFunc> _mappings = [];
    private bool _isFrozen;

    public IReadOnlyDictionary<MapKey, MapFunc> Mappings
    {
        get
        {
            EnsureInitialized();
            return _mappings;
        }
    }

    public void Add(MapperRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            ThrowIfFrozen();
            _mappings[registration.Key] = registration.MappingFunc;
        }
    }

    public void Freeze()
    {
        EnsureInitialized();
    }

    public bool TryGet(MapKey key, out MapFunc? mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureInitialized();
        return _mappings.TryGetValue(key, out mappingFunc);
    }

    private void EnsureInitialized()
    {
        if (_isFrozen)
        {
            return;
        }

        lock (_gate)
        {
            if (_isFrozen)
            {
                return;
            }

            foreach (var registration in Registry.MapperRegistrations)
            {
                _mappings[registration.Key] = registration.MappingFunc;
            }

            _isFrozen = true;
        }
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("MapperInfos is frozen after its first read.");
        }
    }
}

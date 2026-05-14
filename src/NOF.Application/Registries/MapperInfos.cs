using NOF.Abstraction;

namespace NOF.Application;

public sealed class MapperInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<MapperRegistration> _registrations = [];
    private readonly Dictionary<MapKey, MapFunc> _mappings = [];
    private bool _isInitialized;

    public MapperInfos()
        : this(new Registry())
    {
    }

    public MapperInfos(Registry registry)
    {
        _registry = registry;
    }

    public void Add(MapperRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        AddCore(registration);
    }

    public IReadOnlyDictionary<MapKey, MapFunc> Mappings
    {
        get
        {
            EnsureInitialized();
            return _mappings;
        }
    }

    public bool TryGet(MapKey key, out MapFunc? mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureInitialized();
        return _mappings.TryGetValue(key, out mappingFunc);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_initializeGate)
        {
            if (_isInitialized)
            {
                return;
            }

            foreach (var registration in _registry.MapperRegistrations)
            {
                AddCore(registration);
            }

            _registrations.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(MapperRegistration registration)
    {
        _registrations.Add(registration);
        _mappings[registration.Key] = registration.MappingFunc;
    }
}

using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RpcServerInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<RpcServerRegistration> _registrationsList = [];
    private readonly Dictionary<Type, Type> _registrations = [];
    private bool _isInitialized;

    public RpcServerInfos()
        : this(new Registry())
    {
    }

    public RpcServerInfos(Registry registry)
    {
        _registry = registry;
    }

    public void Add(RpcServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        AddCore(registration);
    }

    public IReadOnlyDictionary<Type, Type> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
    }

    public bool TryGetImplementationType(Type contractType, [MaybeNullWhen(false)] out Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        EnsureInitialized();
        return _registrations.TryGetValue(contractType, out implementationType);
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

            foreach (var registration in _registry.RpcServerRegistrations)
            {
                AddCore(registration);
            }

            _registrationsList.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(RpcServerRegistration registration)
    {
        _registrationsList.Add(registration);
        _registrations[registration.ServiceType] = registration.ImplementationType;
    }
}

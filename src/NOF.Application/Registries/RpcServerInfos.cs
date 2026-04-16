using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RpcServerInfos
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, Type> _registrations = [];
    private bool _isFrozen;

    public IReadOnlyDictionary<Type, Type> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
    }

    public void Add(RpcServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            ThrowIfFrozen();
            _registrations[registration.ServiceType] = registration.ImplementationType;
        }
    }

    public void Freeze()
    {
        EnsureInitialized();
    }

    public bool TryGetImplementationType(Type contractType, [MaybeNullWhen(false)] out Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        EnsureInitialized();
        return _registrations.TryGetValue(contractType, out implementationType);
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

            foreach (var registration in Registry.RpcServerRegistrations)
            {
                _registrations[registration.ServiceType] = registration.ImplementationType;
            }

            _isFrozen = true;
        }
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("RpcServerInfos is frozen after its first read.");
        }
    }
}

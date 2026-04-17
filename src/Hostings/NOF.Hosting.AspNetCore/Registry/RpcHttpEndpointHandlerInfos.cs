using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting.AspNetCore;

public sealed class RpcHttpEndpointHandlerInfos
{
    private readonly record struct Key(Type ServiceType, string MethodName);

    private readonly Lock _gate = new();
    private readonly Dictionary<Key, Entry> _registrations = [];
    private bool _isFrozen;

    public sealed record Entry(Delegate Handler, Type ReturnType);

    public IReadOnlyDictionary<(Type ServiceType, string MethodName), Entry> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations.ToDictionary(static kvp => (kvp.Key.ServiceType, kvp.Key.MethodName), static kvp => kvp.Value);
        }
    }

    public void Add(RpcHttpEndpointHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            ThrowIfFrozen();
            _registrations[new Key(registration.ServiceType, registration.MethodName)] = new Entry(registration.Handler, registration.ReturnType);
        }
    }

    public void Freeze()
    {
        EnsureInitialized();
    }

    public bool TryGet(Type serviceType, string methodName, [MaybeNullWhen(false)] out Entry entry)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        EnsureInitialized();
        return _registrations.TryGetValue(new Key(serviceType, methodName), out entry);
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

            foreach (var registration in Registry.RpcHttpEndpointHandlerRegistrations)
            {
                _registrations[new Key(registration.ServiceType, registration.MethodName)] = new Entry(registration.Handler, registration.ReturnType);
            }

            _isFrozen = true;
        }
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("RpcHttpEndpointHandlerInfos is frozen after its first read.");
        }
    }
}


using NOF.Abstraction;

namespace NOF.Application;

public sealed class RequestHandlerInfos
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

    public void Add(RequestHandlerRegistration registration)
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

    public bool TryGetHandlerType(Type serviceType, out Type? handlerType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        EnsureInitialized();
        return _registrations.TryGetValue(serviceType, out handlerType);
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

            foreach (var registration in Registry.RequestHandlerRegistrations)
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
            throw new InvalidOperationException("RequestHandlerInfos is frozen after its first read.");
        }
    }
}

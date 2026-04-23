using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RequestHandlerInfos
{
    private readonly Lock _gate = new();
    public record DynamicallyAccessedPublicConstructorsType(
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type Type);
    private readonly Dictionary<Type, DynamicallyAccessedPublicConstructorsType> _registrations = [];
    private bool _isFrozen;

    public IReadOnlyDictionary<Type, DynamicallyAccessedPublicConstructorsType> Registrations
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
            _registrations[registration.ServiceType] = new(registration.ImplementationType);
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
        if (!_registrations.TryGetValue(serviceType, out var handlerRegistration))
        {
            handlerType = default;
            return false;
        }

        handlerType = handlerRegistration.Type;
        return true;
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
                _registrations[registration.ServiceType] = new(registration.ImplementationType);
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

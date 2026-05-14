using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RequestHandlerInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<RequestHandlerRegistration> _registrationsList = [];
    public record DynamicallyAccessedPublicConstructorsType(
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type Type);
    private readonly Dictionary<Type, DynamicallyAccessedPublicConstructorsType> _registrations = [];
    private bool _isInitialized;

    public RequestHandlerInfos()
        : this(new Registry())
    {
    }

    public RequestHandlerInfos(Registry registry)
    {
        _registry = registry;
    }

    public void Add(RequestHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        AddCore(registration);
    }

    public IReadOnlyDictionary<Type, DynamicallyAccessedPublicConstructorsType> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
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

            foreach (var registration in _registry.RequestHandlerRegistrations)
            {
                AddCore(registration);
            }

            _registrationsList.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(RequestHandlerRegistration registration)
    {
        _registrationsList.Add(registration);
        _registrations[registration.ServiceType] = new(registration.ImplementationType);
    }
}

using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RequestHandlerRegistry : Registry<RequestHandlerRegistration>
{
    public record DynamicallyAccessedPublicConstructorsType(
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type Type);

    private readonly Dictionary<Type, DynamicallyAccessedPublicConstructorsType> _registrationsByService = [];

    public RequestHandlerRegistry()
    {
        Frozen += BuildIndexes;
    }

    public bool TryGetHandlerType(Type serviceType, out Type? handlerType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        Freeze();
        if (!_registrationsByService.TryGetValue(serviceType, out var registration))
        {
            handlerType = null;
            return false;
        }

        handlerType = registration.Type;
        return true;
    }

    private void BuildIndexes()
    {
        _registrationsByService.Clear();
        foreach (var registration in Items)
        {
            Index(registration);
        }
    }

    private void Index(RequestHandlerRegistration registration)
    {
        _registrationsByService[registration.ServiceType] = new(registration.ImplementationType);
    }
}

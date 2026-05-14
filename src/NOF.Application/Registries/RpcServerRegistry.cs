using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public sealed class RpcServerRegistry : Registry<RpcServerRegistration>
{
    private readonly Dictionary<Type, Type> _registrationsByService = [];

    public RpcServerRegistry()
    {
        Frozen += BuildIndexes;
    }

    public bool TryGetImplementationType(Type contractType, [MaybeNullWhen(false)] out Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        Freeze();
        return _registrationsByService.TryGetValue(contractType, out implementationType);
    }

    private void BuildIndexes()
    {
        _registrationsByService.Clear();
        foreach (var registration in Items)
        {
            Index(registration);
        }
    }

    private void Index(RpcServerRegistration registration)
    {
        _registrationsByService[registration.ServiceType] = registration.ImplementationType;
    }
}

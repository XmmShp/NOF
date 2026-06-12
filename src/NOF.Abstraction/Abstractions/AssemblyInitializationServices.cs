using Microsoft.Extensions.DependencyInjection;

namespace NOF.Abstraction;

/// <summary>
/// Helpers for generated assembly initializers that register services and runtime metadata.
/// </summary>
public static class AssemblyInitializationServices
{
    public static T GetOrAddSingleton<T>(IServiceCollection services)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(T) && descriptor.ImplementationInstance is T existing)
            {
                return existing;
            }
        }

        var instance = new T();
        services.AddSingleton(instance);
        return instance;
    }
}

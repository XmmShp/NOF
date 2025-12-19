using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NOF;

public class AutoInjectConfig : IDependentServiceConfig
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        var assemblies = builder.Assemblies;
        var types = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        {
            var autoInjectAttr = type.GetCustomAttribute<AutoInjectAttribute>();
            if (autoInjectAttr is null)
            {
                continue;
            }

            var lifetime = autoInjectAttr.Lifetime;
            var explicitRegisterTypes = autoInjectAttr.RegisterTypes;

            var serviceTypes = explicitRegisterTypes is { Length: > 0 }
                ? explicitRegisterTypes.Where(t => t != type)
                : type.GetInterfaces();

            var serviceTypeList = serviceTypes.ToList();

            if (lifetime == Lifetime.Singleton || lifetime == Lifetime.Scoped)
            {
                if (serviceTypeList.Count == 1)
                {
                    // Single interface: direct registration
                    builder.Services.Add(new ServiceDescriptor(serviceTypeList[0], type, lifetime: Map(lifetime)));
                }
                else
                {
                    // Register concrete type first
                    builder.Services.Add(new ServiceDescriptor(type, type, lifetime: Map(lifetime)));
                    // Then register each interface as a factory pointing to the concrete type
                    foreach (var serviceType in serviceTypeList)
                    {
                        builder.Services.Add(new ServiceDescriptor(serviceType, sp => sp.GetRequiredService(type), lifetime: Map(lifetime)));
                    }
                }
            }
            else // Transient
            {
                if (explicitRegisterTypes is { Length: > 0 })
                {
                    foreach (var serviceType in explicitRegisterTypes)
                    {
                        builder.Services.Add(new ServiceDescriptor(serviceType, type, lifetime: Map(lifetime)));
                    }
                }
                else
                {
                    if (serviceTypeList.Count > 0)
                    {
                        foreach (var serviceType in serviceTypeList)
                        {
                            builder.Services.Add(new ServiceDescriptor(serviceType, type, lifetime: Map(lifetime)));
                        }
                    }
                    else
                    {
                        builder.Services.Add(new ServiceDescriptor(type, type, lifetime: Map(lifetime)));
                    }
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private static ServiceLifetime Map(Lifetime lifetime)
    {
        return lifetime switch
        {
            Lifetime.Singleton => ServiceLifetime.Singleton,
            Lifetime.Scoped => ServiceLifetime.Scoped,
            Lifetime.Transient => ServiceLifetime.Transient,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
        };
    }
}
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Annotation;

namespace NOF.Hosting;

/// <summary>
/// Registers services from source-generated AutoInject metadata.
/// </summary>
public sealed class AutoInjectServiceRegistrationStep : IServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var infos = builder.Services.GetOrAddSingleton<AutoInjectInfos>();
        foreach (var registration in infos.Registrations)
        {
            var lifetime = ToServiceLifetime(registration.Lifetime);
            if (registration.UseFactory)
            {
                builder.Services.Add(new ServiceDescriptor(
                    registration.ServiceType,
                    sp => sp.GetRequiredService(registration.ImplementationType),
                    lifetime));
                continue;
            }

            builder.Services.Add(new ServiceDescriptor(
                registration.ServiceType,
                registration.ImplementationType,
                lifetime));
        }

        return ValueTask.CompletedTask;
    }

    private static ServiceLifetime ToServiceLifetime(Lifetime lifetime)
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

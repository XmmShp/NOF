using Microsoft.Extensions.DependencyInjection;
using NOF.Annotation;

namespace NOF.Infrastructure;

/// <summary>
/// Registers services from source-generated AutoInject metadata.
/// </summary>
public sealed class AutoInjectServiceRegistrationStep : IServiceRegistrationStep<AutoInjectServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        foreach (var registration in AutoInjectRegistry.GetRegistrations())
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

using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers RPC request handlers discovered by source generators as transient services.
/// </summary>
public sealed class RequestHandlerServiceRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        foreach (var registration in builder.Registry.RequestHandlerRegistry.Freeze())
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(registration.ServiceType, registration.ImplementationType));
        }

        return ValueTask.CompletedTask;
    }
}

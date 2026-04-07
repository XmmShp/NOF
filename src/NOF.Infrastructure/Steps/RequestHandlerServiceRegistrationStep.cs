using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers RPC request handlers discovered by source generators as transient services.
/// </summary>
public sealed class RequestHandlerServiceRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        foreach (var registration in RequestHandlerRegistry.GetRegistrations())
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(registration.ServiceType, registration.ImplementationType));
        }

        return ValueTask.CompletedTask;
    }
}


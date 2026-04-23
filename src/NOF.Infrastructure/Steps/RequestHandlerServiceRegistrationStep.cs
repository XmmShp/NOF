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
        var infos = builder.Services.GetOrAddSingleton<RequestHandlerInfos>();
        foreach (var registration in infos.Registrations)
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(registration.Key, registration.Value.Type));
        }

        return ValueTask.CompletedTask;
    }
}

using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

/// <summary>
/// Defines a service-level configuration unit that participates in the DI container registration phase.
/// </summary>
public interface IServiceRegistrationStep : ITopologizable<IServiceRegistrationStep>
{
    ValueTask ExecuteAsync(IHostApplicationBuilder builder);
}

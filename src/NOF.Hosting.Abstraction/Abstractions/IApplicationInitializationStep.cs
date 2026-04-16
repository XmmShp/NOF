using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

/// <summary>
/// Defines an application-level configuration unit that runs after the host application has been fully constructed.
/// </summary>
public interface IApplicationInitializationStep
{
    Task ExecuteAsync(IHost app);
}

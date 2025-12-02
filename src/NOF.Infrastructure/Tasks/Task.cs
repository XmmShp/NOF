using Microsoft.AspNetCore.Builder;

namespace NOF;

public interface ITask;

public interface IDepsOn<TDependency> where TDependency : ITask;

public record RegistrationArgs(WebApplicationBuilder Builder, IDictionary<string, object?> Metadata);

public interface IRegistrationTask : ITask
{
    Task ExecuteAsync(RegistrationArgs builder);
}

public record StartupArgs(WebApplication App, IDictionary<string, object?> Metadata);

public interface IStartupTask : ITask
{
    Task ExecuteAsync(StartupArgs app);
}


using Microsoft.AspNetCore.Builder;

namespace NOF;

public interface IConfigurator;

public interface IDepsOn<TDependency> where TDependency : IConfigurator;

public record RegistrationArgs(WebApplicationBuilder Builder, IDictionary<string, object?> Metadata);

public interface IRegistrationConfigurator : IConfigurator
{
    ValueTask ExecuteAsync(RegistrationArgs args);
}

public record StartupArgs(WebApplication App, IDictionary<string, object?> Metadata);

public interface IStartupConfigurator : IConfigurator
{
    Task ExecuteAsync(StartupArgs args);
}

public interface ICombinedConfigurator : IRegistrationConfigurator, IStartupConfigurator;
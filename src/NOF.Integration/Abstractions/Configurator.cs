using Microsoft.AspNetCore.Builder;

namespace NOF;

public interface IConfigurator;

public interface IDepsOn<TDependency> where TDependency : IConfigurator;

public interface IRegistrationConfigurator : IConfigurator
{
    ValueTask ExecuteAsync(INOFApp app);
}

public interface IStartupConfigurator : IConfigurator
{
    Task ExecuteAsync(INOFApp app, WebApplication webApp);
}